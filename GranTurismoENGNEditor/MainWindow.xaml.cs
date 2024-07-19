using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GranTurismoENGNEditor
{
    public partial class MainWindow : Window
    {
        ENGN engnFile;

        public MainWindow()
        {
            InitializeComponent();

            engnFile = new ENGN();
            engnFile.sampleConfigs.Add(new SampleConfig());
            ResetList();
            lstSamples.SelectedIndex = 0;
        }

        void InputValidation(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        }

        bool SaveValidation(string inputText, Type type)
        {
            if (type == typeof(int))
                return int.TryParse(inputText, out _);

            else if (type == typeof(short))
                return short.TryParse(inputText, out _);

            return false;
        }

        void LoadENGNFile(string path)
        {
            using (Stream stream = File.OpenRead(path))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                string magic = new string(reader.ReadChars(4));
                if (magic != "ENGN")
                {
                    MessageBox.Show("Not a valid ENGN/ES file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else
                {
                    wndMain.Title = $"Gran Turismo ENGN Editor - {Path.GetFileName(path)}";
                    lstSamples.Items.Clear();

                    engnFile = new ENGN();

                    engnFile.fileStartOffset = reader.ReadInt32();
                    engnFile.sampleConfigEndOffset = reader.ReadInt32();
                    engnFile.sampleOffsetReduction = reader.ReadInt32();
                    engnFile.fileSize = reader.ReadInt32();
                    engnFile.sampleConfigEndOffset2 = reader.ReadInt32();
                    engnFile.numSamples = reader.ReadInt32();
                    engnFile.sampleStartOffset = reader.ReadInt32();
                    reader.BaseStream.Position += 16;

                    for (int i = 0; i < engnFile.numSamples; i++)
                    {
                        SampleConfig sampleConfig = new SampleConfig();

                        sampleConfig.centerRpm = reader.ReadInt16();
                        sampleConfig.fadeInRpm = reader.ReadInt16();
                        sampleConfig.fadeOutRpm = reader.ReadInt16();
                        sampleConfig.volume = reader.ReadInt16();
                        sampleConfig.sampleRate = reader.ReadInt32();
                        sampleConfig.sampleDataOffset = reader.ReadInt32();

                        engnFile.sampleConfigs.Add(sampleConfig);
                    }

                    for (int i = 0; i < engnFile.sampleConfigs.Count; i++)
                    {
                        int absoluteOffset = engnFile.sampleConfigEndOffset + engnFile.sampleConfigs[i].sampleDataOffset;
                        reader.BaseStream.Position = absoluteOffset;
                        if (i == engnFile.sampleConfigs.Count - 1)
                        {
                            // Last VAG file in data, read until end
                            engnFile.sampleConfigs[i].vagData = reader.ReadBytes(engnFile.fileSize - engnFile.sampleConfigs[i].sampleDataOffset);
                        }
                        else
                        {
                            engnFile.sampleConfigs[i].vagData = reader.ReadBytes(engnFile.sampleConfigs[i + 1].sampleDataOffset - engnFile.sampleConfigs[i].sampleDataOffset);
                        }

                        ResetList();
                    }
                }
            }
        }

        bool ValidateENGNFile(out string validationMsg)
        {
            foreach (SampleConfig cfg in engnFile.sampleConfigs)
            {
                if (cfg.vagData == null)
                {
                    validationMsg = $"Sample {cfg.centerRpm} has no audio data";
                    return false;
                }
            }
            validationMsg = "";
            return true;
        }

        void SaveENGNFile(string path)
        {
            int cumulativeFileSize = 0; // Combined size of all VAG data
            int vagDataStart = 48 + (engnFile.sampleConfigs.Count * 16); // Start of the first audio data chunk - 48 bytes for ENGN header then however many rows of 16 for sample configs
            string validationMsg = "";

            if (!ValidateENGNFile(out validationMsg))
            {
                MessageBox.Show($"Failed to validate ENGN file:\n{validationMsg}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using (MemoryStream stream = new MemoryStream())
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                bw.Write(Encoding.ASCII.GetBytes("ENGN"));
                bw.Write(0);
                bw.Write(vagDataStart);
                bw.Write(0);

                foreach(SampleConfig config in engnFile.sampleConfigs)
                {
                    config.sampleDataOffset = cumulativeFileSize;
                    cumulativeFileSize += config.vagData.Length;
                }

                bw.Write(cumulativeFileSize);
                bw.Write(vagDataStart);
                bw.Write(engnFile.sampleConfigs.Count);
                bw.Write(48);
                bw.Write(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x7F, 0x00, 0x00, 0x00, 0x7F, 0x00, 0x00, 0x00 });

                for (int i = 0; i < engnFile.sampleConfigs.Count; i++)
                {
                    bw.Write(engnFile.sampleConfigs[i].centerRpm);
                    bw.Write(engnFile.sampleConfigs[i].fadeInRpm);
                    bw.Write(engnFile.sampleConfigs[i].fadeOutRpm);
                    bw.Write(engnFile.sampleConfigs[i].volume);
                    bw.Write(engnFile.sampleConfigs[i].sampleRate);

                    if (i == 0) // If it's the first file then the relative offset is 0, otherwise it's equal to the combined stream data size of the previous files
                        bw.Write(0);
                    else
                        bw.Write(engnFile.sampleConfigs[i].sampleDataOffset);
                        
                }

                // Then just write the VAG data itself in sequence
                foreach (SampleConfig config in engnFile.sampleConfigs)
                {
                    bw.Write(config.vagData);
                }
            }
        }

        void LoadVAGFile(string path, int i)
        {
            using (Stream stream = File.OpenRead(path))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                string magic = new string(reader.ReadChars(3));
                if (magic != "VAG")
                {
                    MessageBox.Show("Not a valid VAG file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else
                {
                    reader.BaseStream.Position = 48; // Jump past header to sound data
                    engnFile.sampleConfigs[i].vagData = reader.ReadBytes((int)stream.Length - 48); // Read to end
                }
            }
            engnFile.sampleConfigs[i].vagFileName = Path.GetFileName(path);
            lblCurrentFile.Content = Path.GetFileName(path);
        }

        void ResetList()
        {
            lstSamples.Items.Clear();

            for (int i = 0; i < engnFile.sampleConfigs.Count; i++)
            {
                ListBoxItem item = new ListBoxItem();
                item.Content = $"{engnFile.sampleConfigs[i].centerRpm}rpm";
                lstSamples.Items.Add(item);
            }
        }

        // Event handlers
        private void btnAddSample_Click(object sender, RoutedEventArgs e)
        {
            int nextIndex = lstSamples.SelectedIndex + 1;

            if (engnFile == null)
                engnFile = new ENGN();

            if (engnFile.sampleConfigs.Count == 0)
                engnFile.sampleConfigs.Add(new SampleConfig());
            else
                engnFile.sampleConfigs.Insert(nextIndex, new SampleConfig());

            ResetList();

            if (engnFile.sampleConfigs.Count == 1)
                lstSamples.SelectedIndex = 0;
            else
                lstSamples.SelectedIndex = nextIndex;
        }
        private void btnImportVag_Click(object sender, RoutedEventArgs e)
        {
            if (lstSamples.SelectedIndex >= 0)
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Filter = "VAG/Sony PS2 Compressed Audio (*.vag)|*.vag";
                if (ofd.ShowDialog() == true)
                    LoadVAGFile(ofd.FileName, lstSamples.SelectedIndex);
            }
        }
        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
                LoadENGNFile(ofd.FileName);
        }
        private void btmRemoveSample_Click(object sender, RoutedEventArgs e)
        {
            if (lstSamples.SelectedIndex >= 0)
            {
                int prevSelect = lstSamples.SelectedIndex;
                engnFile.sampleConfigs.RemoveAt(prevSelect);

                ResetList();

                if (prevSelect < engnFile.sampleConfigs.Count)
                    lstSamples.SelectedIndex = prevSelect;
                else 
                    lstSamples.SelectedIndex = engnFile.sampleConfigs.Count - 1;
            }
        }
        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            if (sfd.ShowDialog() == true)
                SaveENGNFile(sfd.FileName);
        }

        private void lstSamples_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstSamples.SelectedIndex < 0)
            {
                txtRpmCenter.Text = "";
                txtRpmFadeIn.Text = "";
                txtRpmFadeOut.Text = "";
                txtVolume.Text = "";
                txtSampleRate.Text = "";

                return;
            }

            SampleConfig thisSample = engnFile.sampleConfigs[lstSamples.SelectedIndex];
            if (thisSample == null)
                return;

            txtRpmCenter.Text = thisSample.centerRpm.ToString();
            txtRpmFadeIn.Text = thisSample.fadeInRpm.ToString();
            txtRpmFadeOut.Text = thisSample.fadeOutRpm.ToString();
            txtVolume.Text = thisSample.volume.ToString();
            txtSampleRate.Text = thisSample.sampleRate.ToString();

            lblCurrentFile.Content = thisSample.vagFileName;
            //MessageBox.Show($"{engnFile.sampleConfigs[lstSamples.SelectedIndex].centerRpm}rpm");
        }

        private void PropertyChanged(object sender, TextChangedEventArgs e)
        {
            int i = lstSamples.SelectedIndex;
            TextBox source = sender as TextBox;

            if (source == null || source.Text.Trim() == "")
                return;

            try
            {
                switch (source.Name)
                {
                    case "txtRpmCenter":
                        engnFile.sampleConfigs[i].centerRpm = short.Parse(source.Text);
                        if (i >= 0)
                            (lstSamples.SelectedItem as ListBoxItem).Content = $"{source.Text}rpm";
                        break;

                    case "txtRpmFadeIn":
                        engnFile.sampleConfigs[i].fadeInRpm = short.Parse(source.Text);
                        break;

                    case "txtRpmFadeOut":
                        engnFile.sampleConfigs[i].fadeOutRpm = short.Parse(source.Text);
                        break;

                    case "txtVolume":
                        engnFile.sampleConfigs[i].volume = short.Parse(source.Text);
                        break;

                    case "txtSampleRate":
                        engnFile.sampleConfigs[i].sampleRate = int.Parse(source.Text);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                source.Text = "";
            }
        }

        private void btnCopyRpm_Click(object sender, RoutedEventArgs e)
        {
            txtRpmFadeIn.Text = txtRpmCenter.Text;
            txtRpmFadeOut.Text = txtRpmCenter.Text;
        }
    }

    public class ENGN
    {
        public int fileStartOffset, sampleConfigEndOffset, sampleConfigEndOffset2, sampleOffsetReduction, fileSize, numSamples, sampleStartOffset;
        public List<SampleConfig> sampleConfigs = new List<SampleConfig>();
    }
    public class SampleConfig
    {
        public short centerRpm, fadeInRpm, fadeOutRpm, volume;
        public int sampleRate, sampleDataOffset;
        public string vagFileName = "";
        public byte[] vagData;
    }
}

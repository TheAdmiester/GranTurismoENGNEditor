# Gran Turismo ENGN Editor
Allows for the creation and editing of ENGN (aka .es) files, which are containers of multiple loops of Sony HEVAG compressed audio, to allow for custom engine and exhaust sounds in PS1 and PS2 Gran Turismo games. Gran Turismo PSP, 5, and 6 use a newer format called ESGX which is supported by my [GTESGXEditor tool](https://github.com/TheAdmiester/GTESGXEditor).

## Usage
Opening the tool will set up a empty ENGN with one sample present, set to 0rpm. Use the + and - buttons to add new samples to the list, from lowest to highest RPMs. In the sample config section you can specify the following variables for each sample:
- RPM Pitch: The "centre pitch" of the recording. This should equal whatever RPM the sound was recorded at.
- RPM Fade In: Set this to the same as RPM Pitch and the game will automatically blend at at the correct spot. Set it lower in specific cases where you may need it to start the current sample early (e.g. the first sample after a distinctive idle which may not blend nicely)
- RPM Fade Out: Typically the same as RPM Pitch. Your final sample should be set to 32767 so that it doesn't fade out as the engine revs beyond the last sample.
- Volume: Up to 32767. I find that 24000-32000 works nicely for exhausts and 16000-24000 works nicely for engines, but you may want to play with the balance yourself.
- Sample Rate: The Hz of the sample divided by 10, e.g. 24000Hz is 2400. I tend to set these slightly lower, e.g. 2000-2100 for a 24000Hz sound, as sometimes they play back a little high. Keep this consistent between samples.
<br/>
Each sample must be assigned HEVAG audio by clicking the Import VAG Data button and selecting a .vag file. You can create .vag files with certain tools such as Awave Studio.<br/><br/>

HEVAG audio is always saved in multiples of 28 samples, so where you could have a sound that is 2800 samples in length, a sound that is 2804 or 2809 samples long, etc. will be truncated down to 2800. Making these sounds loop seamlessly with no clicks or obvious patterns takes some very time consuming trial and error, and is unfortunately not something I or this tool can help with as it's a limitation of the format.


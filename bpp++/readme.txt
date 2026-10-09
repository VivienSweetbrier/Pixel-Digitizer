	   bpp++
	 by Alcaro
	version 2.1

This tool converts between all graphics modes the SNES supports (2bpp GB, 4bpp SNES, 8bpp SNES, and mode7 (8bpp linear)).
There are two ways to use it.
You can either double-click it and enter the parameters in the console window,
or you can enter the stuff through the command line using this format:
bpp++ [mode in.bin in.bin [offset]]
Mode is two characters. The first is how many BPP the input is, the second is how many BPP you want the output to have.
Valid values are 2, 3, 4, 7, and 8. For example, if you want to convert 4bpp SNES to mode7, you put "47" there (without quotes).
The offset is added to all pixels after decoding the input but before encoding the output.
For example, if it's 42 and the mode is 47 or 48, colors 42 through 51 may appear in the output. The default is 00.
If you don't know what this is about, enter 00.
No credit needed.

Changelog:
1.0 First release, only converts from snes 4bpp to mode7
2.0 Can now convert between any of 2bpp GB, 4bpp SNES, 8bpp SNES, and mode7
2.01 Added instructions for what to do if you don't understand the color offset
2.1 Now supports 3bpp. Useless for 98% of SMWC, but useful for me.
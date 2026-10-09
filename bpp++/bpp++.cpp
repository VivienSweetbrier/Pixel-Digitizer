#include <stdio.h>
#include <string.h>

int main(int argc, char * argv[])
{
	if (argc==2 || argc==3 || argc>5)
	{
		printf("usage:\nbpp++ [mode in out [offset]]");
		return 1;
	}
	FILE * in;
	FILE * out;
	char filename[256];
	char intile[64];
	char decodedtile[64];
	char outtile[64];
	char inmode;
	char outmode;
	int offset=0;
	if (argc==1)
	{
		printf("Enter input BPP (2, 3, 4, 7, or 8): ");
		scanf("%c%c", &inmode, &outmode);
		inmode-='0';
		if (inmode!=2 && inmode!=3 && inmode!=4 && inmode!=7 && inmode!=8)
		{
			printf("Invalid BPP value");
			return 1;
		}
		if (outmode=='\n')
		{
			printf("Enter output BPP (2, 3, 4, 7, or 8): ");
			scanf("%c%*c", &outmode);
		}
		else scanf("%*c");
		outmode-='0';
		if (outmode!=2 && outmode!=3 && outmode!=4 && outmode!=7 && outmode!=8)
		{
			printf("Invalid BPP value");
			return 1;
		}
		printf("Enter input filename: ");
		gets(filename);
		in=fopen(filename, "rb");
		if (!in)
		{
			printf("Couldn't open file");
			return 1;
		}
		printf("Enter output filename: ");
		gets(filename);
		out=fopen(filename, "wb");
		if (!out)
		{
			printf("Couldn't open file");
			return 1;
		}
		printf("Enter color offset in hex (if you don't know what this is, enter 0): ");
		scanf("%x", &offset);
	}
	else
	{
		inmode=argv[1][0]-'0';
		if (inmode!=2 && inmode!=3 && inmode!=4 && inmode!=7 && inmode!=8)
		{
			printf("Invalid BPP value");
			return 1;
		}
		outmode=argv[1][1]-'0';
		if (outmode!=2 && outmode!=3 && outmode!=4 && outmode!=7 && outmode!=8)
		{
			printf("Invalid BPP value");
			return 1;
		}
		in=fopen(argv[2], "rb");
		out=fopen(argv[3], "wb");
		if (!in || !out)
		{
			printf("Couldn't open file");
			return 1;
		}
	}
	if (argc==5)
	{
		sscanf(argv[4], "%x", &offset);
	}
	if (offset<0 || offset>255)
	{
		printf("Invalid color offset");
		return 1;
	}
	while (!feof(in))
	{
		if (inmode==2)
		{
			if (!fread(intile, 1, 16, in)) break;
			for (int xx=0;xx<8;xx++)
			{
				for (int yy=0;yy<8;yy++)
				{
					decodedtile[(yy*8)+7-xx]=
						(( intile[ yy<<1    ]>>xx)&1)    +
						(((intile[(yy<<1)|1 ]>>xx)&1)<<1);
				}
			}
		}
		if (inmode==3)
		{
			if (!fread(intile, 1, 24, in)) break;
			for (int xx=0;xx<8;xx++)
			{
				for (int yy=0;yy<8;yy++)
				{
					decodedtile[(yy*8)+7-xx]=
						(( intile[ yy<<1    ]>>xx)&1)    +
						(((intile[(yy<<1)|1 ]>>xx)&1)<<1)+
						(((intile[ yy    |16]>>xx)&1)<<2);
				}
			}
		}
		if (inmode==4)
		{
			if (!fread(intile, 1, 32, in)) break;
			for (int xx=0;xx<8;xx++)
			{
				for (int yy=0;yy<8;yy++)
				{
					decodedtile[(yy*8)+7-xx]=
						(( intile[ yy<<1    ]>>xx)&1)    +
						(((intile[(yy<<1)|1 ]>>xx)&1)<<1)+
						(((intile[(yy<<1)|16]>>xx)&1)<<2)+
						(((intile[(yy<<1)|17]>>xx)&1)<<3);
				}
			}
		}
		if (inmode==8)
		{
			if (!fread(intile, 1, 64, in)) break;
			for (int xx=0;xx<8;xx++)
			{
				for (int yy=0;yy<8;yy++)
				{
					decodedtile[(yy*8)+7-xx]=
						(( intile[ yy<<1    ]>>xx)&1)    +(((intile[(yy<<1)|1 ]>>xx)&1)<<1)+
						(((intile[(yy<<1)|16]>>xx)&1)<<2)+(((intile[(yy<<1)|17]>>xx)&1)<<3)+
						(((intile[(yy<<1)|32]>>xx)&1)<<4)+(((intile[(yy<<1)|33]>>xx)&1)<<5)+
						(((intile[(yy<<1)|48]>>xx)&1)<<6)+(((intile[(yy<<1)|49]>>xx)&1)<<7);
				}
			}
		}
		if (inmode==7)
		{
			if (!fread(decodedtile, 1, 64, in)) break;
		}
		for (int i=0;i<64;i++) decodedtile[i]+=offset;
		if (outmode==2)
		{
			for (int xx=0;xx<8;xx++)
			{
				outtile[ xx<<1   ]=
					((decodedtile[ xx<<3   ]&1)<<7)|((decodedtile[(xx<<3)|1]&1)<<6)|
					((decodedtile[(xx<<3)|2]&1)<<5)|((decodedtile[(xx<<3)|3]&1)<<4)|
					((decodedtile[(xx<<3)|4]&1)<<3)|((decodedtile[(xx<<3)|5]&1)<<2)|
					((decodedtile[(xx<<3)|6]&1)<<1)| (decodedtile[(xx<<3)|7]&1)    ;
				outtile[(xx<<1)|1]=
					((decodedtile[ xx<<3   ]&2)<<6)|((decodedtile[(xx<<3)|1]&2)<<5)|
					((decodedtile[(xx<<3)|2]&2)<<4)|((decodedtile[(xx<<3)|3]&2)<<3)|
					((decodedtile[(xx<<3)|4]&2)<<2)|((decodedtile[(xx<<3)|5]&2)<<1)|
					 (decodedtile[(xx<<3)|6]&2)    |((decodedtile[(xx<<3)|7]&2)>>1);
			}
			fwrite(outtile, 1, 16, out);
		}
		if (outmode==3)
		{
			for (int xx=0;xx<8;xx++)
			{
				outtile[ xx<<1    ]=
					((decodedtile[ xx<<3   ]&1)<<7)|((decodedtile[(xx<<3)|1]&1)<<6)|
					((decodedtile[(xx<<3)|2]&1)<<5)|((decodedtile[(xx<<3)|3]&1)<<4)|
					((decodedtile[(xx<<3)|4]&1)<<3)|((decodedtile[(xx<<3)|5]&1)<<2)|
					((decodedtile[(xx<<3)|6]&1)<<1)| (decodedtile[(xx<<3)|7]&1)    ;
				outtile[(xx<<1)|1 ]=
					((decodedtile[ xx<<3   ]&2)<<6)|((decodedtile[(xx<<3)|1]&2)<<5)|
					((decodedtile[(xx<<3)|2]&2)<<4)|((decodedtile[(xx<<3)|3]&2)<<3)|
					((decodedtile[(xx<<3)|4]&2)<<2)|((decodedtile[(xx<<3)|5]&2)<<1)|
					 (decodedtile[(xx<<3)|6]&2)    |((decodedtile[(xx<<3)|7]&2)>>1);
				outtile[ xx    |16]=
					((decodedtile[ xx<<3   ]&4)<<5)|((decodedtile[(xx<<3)|1]&4)<<4)|
					((decodedtile[(xx<<3)|2]&4)<<3)|((decodedtile[(xx<<3)|3]&4)<<2)|
					((decodedtile[(xx<<3)|4]&4)<<1)| (decodedtile[(xx<<3)|5]&4)    |
					((decodedtile[(xx<<3)|6]&4)>>1)|((decodedtile[(xx<<3)|7]&4)>>2);
			}
			fwrite(outtile, 1, 24, out);
		}
		if (outmode==4)
		{
			for (int xx=0;xx<8;xx++)
			{
				outtile[ xx<<1    ]=
					((decodedtile[ xx<<3   ]&1)<<7)|((decodedtile[(xx<<3)|1]&1)<<6)|
					((decodedtile[(xx<<3)|2]&1)<<5)|((decodedtile[(xx<<3)|3]&1)<<4)|
					((decodedtile[(xx<<3)|4]&1)<<3)|((decodedtile[(xx<<3)|5]&1)<<2)|
					((decodedtile[(xx<<3)|6]&1)<<1)| (decodedtile[(xx<<3)|7]&1)    ;
				outtile[(xx<<1)|1 ]=
					((decodedtile[ xx<<3   ]&2)<<6)|((decodedtile[(xx<<3)|1]&2)<<5)|
					((decodedtile[(xx<<3)|2]&2)<<4)|((decodedtile[(xx<<3)|3]&2)<<3)|
					((decodedtile[(xx<<3)|4]&2)<<2)|((decodedtile[(xx<<3)|5]&2)<<1)|
					 (decodedtile[(xx<<3)|6]&2)    |((decodedtile[(xx<<3)|7]&2)>>1);
				outtile[(xx<<1)|16]=
					((decodedtile[ xx<<3   ]&4)<<5)|((decodedtile[(xx<<3)|1]&4)<<4)|
					((decodedtile[(xx<<3)|2]&4)<<3)|((decodedtile[(xx<<3)|3]&4)<<2)|
					((decodedtile[(xx<<3)|4]&4)<<1)| (decodedtile[(xx<<3)|5]&4)    |
					((decodedtile[(xx<<3)|6]&4)>>1)|((decodedtile[(xx<<3)|7]&4)>>2);
				outtile[(xx<<1)|17]=
					((decodedtile[ xx<<3   ]&8)<<4)|((decodedtile[(xx<<3)|1]&8)<<3)|
					((decodedtile[(xx<<3)|2]&8)<<2)|((decodedtile[(xx<<3)|3]&8)<<1)|
					 (decodedtile[(xx<<3)|4]&8)    |((decodedtile[(xx<<3)|5]&8)>>1)|
					((decodedtile[(xx<<3)|6]&8)>>2)|((decodedtile[(xx<<3)|7]&8)>>3);
			}
			fwrite(outtile, 1, 32, out);
		}
		if (outmode==8)
		{
			for (int xx=0;xx<8;xx++)
			{
				outtile[ xx<<1    ]=
					((decodedtile[ xx<<3   ]&1  )<<7)|((decodedtile[(xx<<3)|1]&1  )<<6)|
					((decodedtile[(xx<<3)|2]&1  )<<5)|((decodedtile[(xx<<3)|3]&1  )<<4)|
					((decodedtile[(xx<<3)|4]&1  )<<3)|((decodedtile[(xx<<3)|5]&1  )<<2)|
					((decodedtile[(xx<<3)|6]&1  )<<1)| (decodedtile[(xx<<3)|7]&1  )    ;
				outtile[(xx<<1)|1 ]=
					((decodedtile[ xx<<3   ]&2  )<<6)|((decodedtile[(xx<<3)|1]&2  )<<5)|
					((decodedtile[(xx<<3)|2]&2  )<<4)|((decodedtile[(xx<<3)|3]&2  )<<3)|
					((decodedtile[(xx<<3)|4]&2  )<<2)|((decodedtile[(xx<<3)|5]&2  )<<1)|
					 (decodedtile[(xx<<3)|6]&2  )    |((decodedtile[(xx<<3)|7]&2  )>>1);
				outtile[(xx<<1)|16]=
					((decodedtile[ xx<<3   ]&4  )<<5)|((decodedtile[(xx<<3)|1]&4  )<<4)|
					((decodedtile[(xx<<3)|2]&4  )<<3)|((decodedtile[(xx<<3)|3]&4  )<<2)|
					((decodedtile[(xx<<3)|4]&4  )<<1)| (decodedtile[(xx<<3)|5]&4  )    |
					((decodedtile[(xx<<3)|6]&4  )>>1)|((decodedtile[(xx<<3)|7]&4  )>>2);
				outtile[(xx<<1)|17]=
					((decodedtile[ xx<<3   ]&8  )<<4)|((decodedtile[(xx<<3)|1]&8  )<<3)|
					((decodedtile[(xx<<3)|2]&8  )<<2)|((decodedtile[(xx<<3)|3]&8  )<<1)|
					 (decodedtile[(xx<<3)|4]&8  )    |((decodedtile[(xx<<3)|5]&8  )>>1)|
					((decodedtile[(xx<<3)|6]&8  )>>2)|((decodedtile[(xx<<3)|7]&8  )>>3);
				outtile[(xx<<1)|32]=
					((decodedtile[ xx<<3   ]&16 )<<3)|((decodedtile[(xx<<3)|1]&16 )<<2)|
					((decodedtile[(xx<<3)|2]&16 )<<1)| (decodedtile[(xx<<3)|3]&16 )    |
					((decodedtile[(xx<<3)|4]&16 )>>1)|((decodedtile[(xx<<3)|5]&16 )>>2)|
					((decodedtile[(xx<<3)|6]&16 )>>3)|((decodedtile[(xx<<3)|7]&16 )>>4);
				outtile[(xx<<1)|33]=
					((decodedtile[ xx<<3   ]&32 )<<2)|((decodedtile[(xx<<3)|1]&32 )<<1)|
					 (decodedtile[(xx<<3)|2]&32 )    |((decodedtile[(xx<<3)|3]&32 )>>1)|
					((decodedtile[(xx<<3)|4]&32 )>>2)|((decodedtile[(xx<<3)|5]&32 )>>3)|
					((decodedtile[(xx<<3)|6]&32 )>>4)|((decodedtile[(xx<<3)|7]&32 )>>5);
				outtile[(xx<<1)|48]=
					((decodedtile[ xx<<3   ]&64 )<<1)| (decodedtile[(xx<<3)|1]&64 )    |
					((decodedtile[(xx<<3)|2]&64 )>>1)|((decodedtile[(xx<<3)|3]&64 )>>2)|
					((decodedtile[(xx<<3)|4]&64 )>>3)|((decodedtile[(xx<<3)|5]&64 )>>4)|
					((decodedtile[(xx<<3)|6]&64 )>>5)|((decodedtile[(xx<<3)|7]&64 )>>6);
				outtile[(xx<<1)|49]=
					 (decodedtile[ xx<<3   ]&128)    |((decodedtile[(xx<<3)|1]&128)>>1)|
					((decodedtile[(xx<<3)|2]&128)>>2)|((decodedtile[(xx<<3)|3]&128)>>3)|
					((decodedtile[(xx<<3)|4]&128)>>4)|((decodedtile[(xx<<3)|5]&128)>>5)|
					((decodedtile[(xx<<3)|6]&128)>>6)|((decodedtile[(xx<<3)|7]&128)>>7);
			}
			fwrite(outtile, 1, 64, out);
		}
		if (outmode==7)
		{
			fwrite(decodedtile, 1, 64, out);
		}
	}
	fclose(in);
	fclose(out);
	return 0;
}

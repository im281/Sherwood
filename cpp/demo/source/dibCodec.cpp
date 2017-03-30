#include "dibCodec.h"

#include <fstream>

namespace MicrosoftResearch { namespace Cambridge { namespace Sherwood
{
#pragma pack(push,1)  // push current alignment to stack 
#pragma pack(1)     // set alignment to 1 byte boundary 

  typedef struct BitmapFileHeader_
  {
    unsigned short  bfType; 
    unsigned int    bfSize; 
    unsigned short  bfReserved1; 
    unsigned short  bfReserved2; 
    unsigned int    bfOffBits; 
  } BitmapFileHeader; 

  struct BitmapInfoHeader
  {
    unsigned int    biSize; 
    int             biWidth; 
    int             biHeight; 
    unsigned short  biPlanes; 
    unsigned short  biBitCount; 
    unsigned int    biCompression; 
    unsigned int    biSizeImage; 
    int             biXPelsPerMeter; 
    int             biYPelsPerMeter; 
    unsigned int    biClrUsed; 
    unsigned int    biClrImportant; 
  };

#pragma pack(pop)

  struct RgbQuad
  {
    unsigned char rgbBlue; 
    unsigned char rgbGreen; 
    unsigned char rgbRed; 
    unsigned char rgbReserved; 
  };

  void encodeDib_BGR_8u (
    const unsigned char* input,
    int width, 
    int height,
    int rowStepBytes,
    const char* path )
  {
    // DIBs have rows padded to four byte boundaries, the input image may not
    int dwPaddedRowStepBytes = 3*width + (4-(3*width%4))%4;

    BitmapFileHeader h;
    h.bfType = 19778;  // "BM"
    h.bfSize = sizeof(BitmapFileHeader) + sizeof(BitmapInfoHeader) + height*dwPaddedRowStepBytes; 
    h.bfReserved1 = 0; 
    h.bfReserved2 = 0; 
    h.bfOffBits = sizeof(BitmapFileHeader) + sizeof(BitmapInfoHeader);

    BitmapInfoHeader b;

    b.biSize = sizeof(BitmapInfoHeader);
    b.biWidth = width;
    b.biHeight = height;
    b.biPlanes = 1;
    b.biBitCount = 24;
    b.biCompression = 0;    // uncompressed
    b.biSizeImage = dwPaddedRowStepBytes*height;
    b.biXPelsPerMeter = 0;  // we don't know
    b.biYPelsPerMeter = 0;  // we don't know
    b.biClrUsed = 0; 
    b.biClrImportant = 0;     // all colors required

    std::ofstream o;
    o.open(path, std::ios_base::out | std::ios_base::binary);

    // Write file header
    o.write((char*)&h, sizeof(BitmapFileHeader));

    // Write bitmap header
    o.write((char*)&b, sizeof(BitmapInfoHeader));

    // Write pixels
    for(int v=0; v<height; v++)
    {
      o.write((char*)(input + (height-v-1)*rowStepBytes), 3*width);

      // Pad with zeros to long word boundary
      for(int i=0; i<dwPaddedRowStepBytes-3*width; i++)
      {
        char zero = 0;       
        o.write(&zero, 1);
      }
    }

    o.close();  // actually happens in destructor anyway but some people don't realise
  }
} } }

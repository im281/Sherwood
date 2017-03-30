#pragma once

#include <cstdlib>

namespace MicrosoftResearch { namespace Cambridge { namespace Sherwood
{
  /// A very simple graphics context for drawing lines and rectanlges on
  // bitmaps without the need for bloated libraries or platform-specific code.
  template<class PixelType>
  class Graphics
  {
    unsigned char* data_;
    int width_, height_, stride_;

  public:
    Graphics(unsigned char* data, int width, int height, int stride):
        data_(data), width_(width), height_(height), stride_(stride)
        {
        }

        void FillRectangle(const PixelType& color, int x0, int y0, int width, int height)
        {
          for(int v=y0; v<y0 + height; v++)
          {
            unsigned char* p = data_ + (v*stride_) + x0*sizeof(PixelType);

            for(int u=0; u<width; u++)
            {
              *((PixelType*)(p)) = color;
              p += sizeof(PixelType);
            }
          }
        }

        void FillRectangle(const PixelType& color, float x0, float y0, float width, float height)
        {
          // Just a placeholder until we can implement anti-aliased rectangle drawing
          FillRectangle(color, (int)(x0+0.5f), (int)(y0+0.5f), (int)(width+0.5f), (int)(height+0.5f));
        }

        void DrawLine(const PixelType& color, int x0, int y0, int x1, int y1)
        {
          // Adapted from Computer Graphics: Principles and Practice, Second Edition in C, Section 3.18, pp. 141.
          unsigned char* addr = data_ + (y0*stride_) + x0*sizeof(PixelType);

          int dx = x1-x0;
          int dy = y1-y0;

          int u;
          int du;
          int uincr;

          if (std::abs(dx) > std::abs(dy))
          {
            du = std::abs(dx);
            u = x1;
            uincr = sizeof(PixelType);
            if (dx < 0)
              uincr = -uincr;
          }
          else
          {
            du = std::abs(dy);
            u = y1;
            uincr = stride_;
            if (dy < 0)
              uincr = -uincr;
          }
          int uend = u + du;
          do
          {
            *((PixelType*)(addr)) = color;

            u = u+1;
            addr = addr+uincr;
          } while (u < uend);
        }

        void DrawLine(const PixelType& color, float x0, float y0, float x1, float y1)
        {
          // Just a placeholder until we can implement anti-aliased line drawing
          DrawLine(color, (int)(x0+0.5f), (int)(y0+0.5f), (int)(x1+0.5f), (int)(y1+0.5f));
        }

        void DrawRectangle(const PixelType& color, int x0, int y0, int width, int height)
        {
          DrawLine(color, x0,y0,x0+width, y0);
          DrawLine(color, x0+width, y0, x0+width, y0+height);
          DrawLine(color, x0+width, y0+height, x0, y0+height);
          DrawLine(color, x0, y0+width, x0, y0);
        }

        void DrawRectangle(const PixelType& color, float x0, float y0, float width, float height)
        {
          // Just a placeholder until we can implement anti-aliased rectangle drawing
          DrawRectangle(color, (int)(x0+0.5f), (int)(y0+0.5f), (int)(width+0.5f), (int)(height+0.5f));
        }
  };
} } }

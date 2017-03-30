#pragma once

#include <string>
#include <utility>
#include <vector>

namespace MicrosoftResearch { namespace Cambridge { namespace Sherwood
{
  // As series of classes needed to replace their equivalents in C# - to
  // facilitate more closely parallel C++ and C# implementation...

  struct Size
  {
    Size(int width, int height):Width(width), Height(height) {}

    int Width, Height;
  };

  struct PointF
  {
    PointF(float x, float y):X(x), Y(y) {}
    float X, Y;
  };

  struct RectangleF
  {
    float X, Y, Width, Height;
    RectangleF(float x, float y, float width, float height):
    X(x), Y(y), Width(width), Height(height)
    {

    }
  };

  struct PixelBgr
  {
    static PixelBgr FromArgb(unsigned char r, unsigned char g, unsigned char b){
      PixelBgr color;
      //color.A = 255;
      color.R = r;
      color.G = g;
      color.B = b;

      return color;
    }

    unsigned char B,G,R;
  };

  // A bitmap image, templated on pixel type.
  template<class PixelType>
  class Bitmap
  {
  private:
    unsigned char* buffer_;

    int width_, height_, stride_;

    static int computeStrideBytes(int width, int padMultiple)
    {
      // There is a faster way of doing this with bit shifting but I have
      // forgotten what it is!
      int multiplier = width*sizeof(PixelType)/padMultiple; // truncate
      if(width*sizeof(PixelType)%padMultiple!=0)
        multiplier += 1;
      return multiplier*padMultiple;
    }

  public:
    Bitmap(int width, int height)
    {
      width_ = width;
      height_ = height;

      stride_ = Bitmap::computeStrideBytes(width_, 4); // pad rows to multiple of four bytes

      buffer_=  new unsigned char[height * width * stride_];
    }

    ~Bitmap()
    {
      delete[] buffer_;
    }

    unsigned char* GetBuffer()
    {
      return &buffer_[0];
    }

    int GetStride() const
    {
      return stride_;
    }

    int GetWidth() const
    {
      return width_;
    }

    int GetHeight() const
    {
      return height_;
    }

    void SetPixel(int u, int v, PixelBgr color)
    {
      *(PixelBgr*)(&buffer_[v*width_*sizeof(PixelBgr) + u*sizeof(PixelBgr)]) = color;
    }

    void Save(const std::string& path) const;
  };

  // Compute the 'best fit' plot range given the data range, the plot
  // dimensions, and a padding parameter.
  class PlotCanvas
  {
  public:
    std::pair<float, float> plotRangeX, plotRangeY;
    float stepX, stepY;

    PlotCanvas(std::pair<float, float> dataRangeX, std::pair<float, float> dataRangeY, Size PlotSize, PointF padding)
    {
      float dataExtentX = dataRangeX.second - dataRangeX.first;
      float dataExtentY = dataRangeY.second - dataRangeY.first;

      // Expand the plot dimension compared to the data range for a better visualization.
      plotRangeX = std::pair<float, float>(dataRangeX.first - dataExtentX * padding.X, dataRangeX.second + dataExtentX * padding.X);
      plotRangeY = std::pair<float, float>(dataRangeY.first - dataExtentY * padding.Y, dataRangeY.second + dataExtentY *padding.Y);

      // Scale the plot to fit into the plot bounding box
      if ((plotRangeX.second - plotRangeX.first) / PlotSize.Width > (plotRangeY.second - plotRangeY.first) / PlotSize.Height)
      {
        float scale = (plotRangeX.second - plotRangeX.first) / PlotSize.Width;
        float midRangeY = (plotRangeY.second + plotRangeY.first) / 2.0f;
        float extentY = scale * PlotSize.Height;
        plotRangeY = std::pair<float, float>(midRangeY - extentY / 2.0f, midRangeY + extentY / 2.0f);
      }
      else
      {
        float scale = (plotRangeY.second - plotRangeY.first) / PlotSize.Height;
        float midRangeX = (plotRangeX.second + plotRangeX.first) / 2.0f;
        float extentX = scale * PlotSize.Width;
        plotRangeX = std::pair<float, float>(midRangeX - extentX / 2.0f, midRangeX + extentX / 2.0f);
      }

      stepX = (plotRangeX.second - plotRangeX.first) / PlotSize.Width;
      stepY = (plotRangeY.second - plotRangeY.first) / PlotSize.Height;
    }
  };
} } }

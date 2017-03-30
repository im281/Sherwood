using System;
using System.Drawing;
using System.Collections.Generic;

namespace MicrosoftResearch.Cambridge.Sherwood
{
  class Tuple<T1, T2>
  {
    public T1 Item1;
    public T2 Item2;
    public Tuple(T1 t1, T2 t2)
    {
      Item1 = t1;
      Item2 = t2;
    }
  }

  // Compute the 'best fit' plot range given the data range, the plot
  // dimensions, and a padding parameter.
  class PlotCanvas
  {
    public Tuple<float, float> plotRangeX, plotRangeY;
    public float stepX, stepY;

    public PlotCanvas(Tuple<float, float> dataRangeX, Tuple<float, float> dataRangeY, Size PlotSize, PointF padding)
    {
      float dataExtentX = dataRangeX.Item2 - dataRangeX.Item1;
      float dataExtentY = dataRangeY.Item2 - dataRangeY.Item1;

      // Expand the plot dimension compared to the data range for a better visualization.
      plotRangeX = new Tuple<float, float>(dataRangeX.Item1 - dataExtentX * padding.X, dataRangeX.Item2 + dataExtentX * padding.X);
      plotRangeY = new Tuple<float, float>(dataRangeY.Item1 - dataExtentY * padding.Y, dataRangeY.Item2 + dataExtentY * padding.Y);

      // Scale the plot to fit into the plot bounding box
      if ((plotRangeX.Item2 - plotRangeX.Item1) / PlotSize.Width > (plotRangeY.Item2 - plotRangeY.Item1) / PlotSize.Height)
      {
        float scale = (plotRangeX.Item2 - plotRangeX.Item1) / PlotSize.Width;
        float midRangeY = (plotRangeY.Item2 + plotRangeY.Item1) / 2.0f;
        float extentY = scale * PlotSize.Height;
        plotRangeY = new Tuple<float, float>(midRangeY - extentY / 2.0f, midRangeY + extentY / 2.0f);
      }
      else
      {
        float scale = (plotRangeY.Item2 - plotRangeY.Item1) / PlotSize.Height;
        float midRangeX = (plotRangeX.Item2 + plotRangeX.Item1) / 2.0f;
        float extentX = scale * PlotSize.Width;
        plotRangeX = new Tuple<float, float>(midRangeX - extentX / 2.0f, midRangeX + extentX / 2.0f);
      }

      stepX = (plotRangeX.Item2 - plotRangeX.Item1) / PlotSize.Width;
      stepY = (plotRangeY.Item2 - plotRangeY.Item1) / PlotSize.Height;

    }
  }

}
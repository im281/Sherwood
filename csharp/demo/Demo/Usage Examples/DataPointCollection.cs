using System;
using System.Collections.Generic;

namespace MicrosoftResearch.Cambridge.Sherwood
{
  /// <summary>
  /// Used to describe the expected format of the lines of a data file (used
  /// in DataPointCollection.Load()).
  /// </summary>
  [Flags]
  enum DataDescriptor
  {
    Unadorned = 0x0,
    HasClassLabels = 0x1,
    HasTargetValues = 0x2
  }

  /// <summary>
  /// A collection of data points, each represented by float[] and (optionally)
  /// associated with a string class label and/or a float target value.
  /// </summary>
  class DataPointCollection : IDataPointCollection
  {
    public const int UnknownClassLabel = -1;

    // Actually not a good way of storing data! We'd prefer to use a single
    // contiguous array (more efficient cache utilization, less work for the
    // garbage collector, etc.) but the absence of pointers in C# would make
    // it hard to provide calling code with a reference to a data point (we'd
    // probably have to 
    // would then have to reference data points via a cumbersome combination
    // of an array reference and an array index. Obviously possible, but
    // wouldn't improve code clarify and so we have not done so here. The C++
    // version of this library uses a more efficient approach.
    List<float[]> data_;

    int dimension_;

    // only for classified data...
    List<int> labels_;

    Dictionary<string, int> labelIndices_ = new Dictionary<string, int>(); // map string labels to integers

    // only for regression problems...
    List<float> targets_;

    /// <summary>
    /// Load a collection of data from a tab-delimited file with one data point
    /// per line. The data may optionally have associated with class labels
    /// (first element on line) and/or target values (last element on line).
    /// </summary>
    /// <param name="path">Path of file to be read.</param>
    /// <param name="bHasClassLabels">Are the data associated with class labels?</param>
    /// <param name="dataDimension">Dimension of the data (excluding class labels and target values).</param>
    /// <param name="bHasTargetValues">Are the data associated with target values.</param>
    static public DataPointCollection Load(System.IO.Stream stream, int dataDimension, DataDescriptor descriptor)
    {
      bool bHasTargetValues = (descriptor & DataDescriptor.HasTargetValues) == DataDescriptor.HasTargetValues;
      bool bHasClassLabels = (descriptor & DataDescriptor.HasClassLabels) == DataDescriptor.HasClassLabels;

      DataPointCollection result = new DataPointCollection();
      result.data_ = new List<float[]>();
      result.labels_ = bHasClassLabels ? new List<int>() : null;
      result.targets_ = bHasTargetValues ? new List<float>() : null;
      result.dimension_ = dataDimension;

      char[] seperators = new char[] { '\t' };

      int elementsPerLine = (bHasClassLabels ? 1 : 0) + dataDimension + (bHasTargetValues ? 1 : 0);

      using (System.IO.StreamReader r = new System.IO.StreamReader(stream))
      {
        string line;
        while ((line = r.ReadLine()) != null)
        {
          string[] elements = line.Split(seperators);

          if (elements.Length != elementsPerLine)
            throw new Exception("Encountered line with unexpected number of elements.");

          int index = 0;

          if (bHasClassLabels)
          {
            if (!String.IsNullOrEmpty(elements[index]))
            {
              if (!result.labelIndices_.ContainsKey(elements[index]))
                result.labelIndices_.Add(elements[index], result.labelIndices_.Count);
              result.labels_.Add(result.labelIndices_[elements[index++]]);
            }
            else
            {
              result.labels_.Add(UnknownClassLabel);
              index++;
            }
          }

          float[] datum = new float[dataDimension];
          for (int i = 0; i < dataDimension; i++)
            datum[i] = Convert.ToSingle(elements[index++]);

          result.data_.Add(datum);

          if (bHasTargetValues)
            result.targets_.Add(Convert.ToSingle(elements[index++]));
        }
      }

      return result;
    }

    /// <summary>
    /// Generate a 2D dataset with data points distributed in a grid pattern.
    /// Intended for generating visualization images.
    /// </summary>
    /// <param name="rangeX">x-axis range</param>
    /// <param name="nStepsX">Number of grid points in x direction</param>
    /// <param name="rangeY">y-axis range</param>
    /// <param name="nStepsY">Number of grid points in y direction</param>
    /// <returns>A new DataPointCollection</returns>
    static public DataPointCollection Generate2dGrid(
        Tuple<float, float> rangeX, int nStepsX,
        Tuple<float, float> rangeY, int nStepsY)
    {

      if (rangeX.Item1 >= rangeX.Item2)
        throw new ArgumentException("Invalid x-axis range.");
      if (rangeY.Item1 >= rangeY.Item2)
        throw new ArgumentException("Invalid y-axis range.");

      DataPointCollection result = new DataPointCollection();

      result.dimension_ = 2;
      result.data_ = new List<float[]>();

      float stepX = (rangeX.Item2 - rangeX.Item1) / nStepsX;
      float stepY = (rangeY.Item2 - rangeY.Item1) / nStepsY;

      for (int j = 0; j < nStepsY; j++)
        for (int i = 0; i < nStepsX; i++)
          result.data_.Add(new float[] { rangeX.Item1 + i * stepX, rangeY.Item1 + j * stepY });

      return result;
    }

    /// <summary>
    /// Generate a 1D dataset containing a given number of data points
    /// distributed at regular intervals within a given range. Intended for
    /// generating visualization images.
    /// </summary>
    /// <param name="range">Range</param>
    /// <param name="nStepsX">Number of grid points</param>
    /// <returns>A new DataPointCollection</returns>
    static public DataPointCollection Generate1dGrid(Tuple<float, float> range, int nSteps)
    {
      if (range.Item1 >= range.Item2)
        throw new ArgumentException("Invalid range.");

      DataPointCollection result = new DataPointCollection();

      result.dimension_ = 1;
      result.data_ = new List<float[]>();

      float step = (range.Item2 - range.Item1) / nSteps;

      for (int i = 0; i < nSteps; i++)
        result.data_.Add(new float[] { range.Item1 + i * step });

      return result;
    }

    /// <summary>
    /// Do these data have class labels?
    /// </summary>
    public bool HasLabels
    {
      get
      {
        return labels_ != null;
      }
    }

    /// <summary>
    /// How many unique class labels are there?
    /// </summary>
    /// <returns>The number of unique class labels</returns>
    public int CountClasses()
    {
      if (!this.HasLabels)
        throw new Exception("Unlabelled data.");
      return (int)(labelIndices_.Count);
    }

    /// <summary>
    /// Do these data have target values (e.g. for regression)?
    /// </summary>
    public bool HasTargetValues
    {
      get
      {
        return targets_ != null;
      }
    }

    /// <summary>
    /// Count the data points in this collection.
    /// </summary>
    /// <returns>The number of data points</returns>
    public int Count()
    {
      return data_.Count;
    }

    /// <summary>
    /// Get the data range in the specified data dimension.
    /// </summary>
    /// <param name="dimension">The dimension over which to compute min and max</param>
    /// <returns>A tuple containing min and max over the specified dimension of the data</returns>
    public Tuple<float, float> GetRange(int dimension)
    {
      if (Count() < 0)
        throw new Exception("Insufficient data to compute range.");

      float min = data_[0][dimension], max = data_[0][dimension];

      for (int i = 0; i < data_.Count; i++)
      {
        if (data_[i][dimension] < min)
          min = data_[i][dimension];
        else if (data_[i][dimension] > max)
          max = data_[i][dimension];
      }

      return new Tuple<float, float>(min, max);
    }

    /// <summary>
    /// Get the range of target values (or raise an exception if these data
    /// do not have associated target values).
    /// </summary>
    /// <returns>A tuple containing the min and max target value for the data</returns>
    public Tuple<float, float> GetTargetRange()
    {
      if (!HasTargetValues)
        throw new Exception("Data points do not have target values.");
      if (Count() < 0)
        throw new Exception("Insufficient data to compute range.");

      float min = targets_[0], max = targets_[0];

      for (int i = 0; i < data_.Count; i++)
      {
        if (targets_[i] < min)
          min = targets_[i];
        else if (targets_[i] > max)
          max = targets_[i];
      }

      return new Tuple<float, float>(min, max);
    }

    /// <summary>
    /// The dimensionality of the data (excluding optional target values).
    /// </summary>
    public int Dimensions
    {
      get
      {
        return dimension_;
      }
    }

    /// <summary>
    /// Get the specified data point.
    /// </summary>
    /// <param name="i">Zero-based data point index.</param>
    /// <returns></returns>
    public float[] GetDataPoint(int i)
    {
      return data_[i];
    }

    /// <summary>
    /// Get the class label for the specified data point (or raise an
    /// exception if these data points do not have associated labels).
    /// </summary>
    /// <param name="i">Zero-based data point index</param>
    /// <returns>A zero-based integer class label.</returns>
    public int GetIntegerLabel(int i)
    {
      if (!HasLabels)
        throw new Exception("Data have no associated class labels.");

      return labels_[i]; // may throw IndexOutOfRangeException
    }

    /// <summary>
    /// Get the target value for the specified data point (or raise an
    /// exception if these data points do not have associated target values).
    /// </summary>
    /// <param name="i">Zero-based data point index.</param>
    /// <returns>The target value.</returns>
    public float GetTarget(int i)
    {
      if (!HasTargetValues)
        throw new Exception("Data have no associated target values.");

      return targets_[i]; // may throw IndexOutOfRangeException
    }
  }
}
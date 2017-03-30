// This file defines some IFeature implementations used by the example code in
// Classification.cs, DensityEstimation.cs, etc. Note we represent IFeature
// instances using simple structs so that all tree data can be stored
// contiguously in a linear array.

using System;
using System.Collections.Generic;

namespace MicrosoftResearch.Cambridge.Sherwood
{
  /// <summary>
  /// A feature that orders data points using one of their coordinates,
  /// i.e. by projecting them onto a coordinate axis.
  /// </summary>
  [Serializable]
  class AxisAlignedFeatureResponse : IFeatureResponse
  {
    int axis_;

    /// <summary>
    /// Create an AxisAlignedFeature instance for the specified axis.
    /// </summary>
    /// <param name="axis">The zero-based index of the axis.</param>
    public AxisAlignedFeatureResponse(int axis)
    {
      axis_ = axis;
    }

    /// <summary>
    /// Create an AxisAlignedFeature instance with a random choice of axis.
    /// </summary>
    /// <param name="randomNumberGenerator">A random number generator.</param>
    /// <returns>A new AxisAlignedFeature instance.</returns>
    public static AxisAlignedFeatureResponse CreateRandom(Random random)
    {
      return new AxisAlignedFeatureResponse(random.Next(0, 2));
    }

    public int Axis
    {
      get { return axis_; }
    }

    #region IFeature
    public float GetResponse(IDataPointCollection data, int sampleIndex)
    {
      DataPointCollection concreteData = (DataPointCollection)(data);
      return concreteData.GetDataPoint((int)sampleIndex)[axis_];
    }

    public override string ToString()
    {
      return String.Format("AxisAlignedFeatureResponse({0})", axis_);
    }
    #endregion
  }

  /// <summary>
  /// A feature that orders data points using a linear combination of their
  /// coordinates, i.e. by projecting them onto a given direction vector.
  /// </summary>
  [Serializable]
  class LinearFeatureResponse2d : IFeatureResponse
  {
    float dx_, dy_;

    /// <summary>
    /// Create a LinearFeature2d instance for the specified direction vector.
    /// </summary>
    /// <param name="dx">The first element of the direction vector.</param>
    /// <param name="dx">The second element of the direction vector.</param> 
    public LinearFeatureResponse2d(float dx, float dy)
    {
      dx_ = dx; dy_ = dy;
    }

    /// <summary>
    /// Create a LinearFeature2d instance with a random direction vector.
    /// </summary>
    /// <param name="randomNumberGenerator">A random number generator.</param>
    /// <returns>A new LinearFeature2d instance.</returns>
    public static LinearFeatureResponse2d CreateRandom(Random random)
    {
      double dx = 2.0 * random.NextDouble() - 1.0;
      double dy = 2.0 * random.NextDouble() - 1.0;

      double magnitude = Math.Sqrt(dx * dx + dy * dy);

      return new LinearFeatureResponse2d((float)(dx / magnitude), (float)(dy / magnitude));
    }

    #region IFeatureImplementation
    public float GetResponse(IDataPointCollection data, int index)
    {
      DataPointCollection concreteData = (DataPointCollection)(data);
      return dx_ * concreteData.GetDataPoint((int)index)[0] + dy_ * concreteData.GetDataPoint((int)index)[1];
    }

    public override string ToString()
    {
      return String.Format("LinearFeatureResponse({0},{1})", dx_, dy_);
    }
    #endregion
  }
}
// This file defines some IStatisticsAggregator implementations used by the
// example code in Classification.cs, DensityEstimation.cs, etc. Note we
// represent IStatisticsAggregator instances using simple structs so that all
// tree data can be stored contiguously in a linear array.

using System;
using System.Collections.Generic;

namespace MicrosoftResearch.Cambridge.Sherwood
{
  class HistogramData
  {
    private List<ushort> binData_;
    int nBins_;

    public HistogramData(int nBins)
    {
      nBins_ = nBins;
    }

    public int GetMemory()
    {
      // Bad implementation - not exception safe
      for (int b = 0; b < nBins_; b++)
        binData_.Add(0);

      return binData_[binData_.Count - nBins_];
    }

    public double Entropy(int handle, int sampleCount_)
    {
      if (sampleCount_ == 0)
        return 0.0;

      double result = 0.0;
      for (int b = handle; b <handle+nBins_; b++)
      {
        double p = (double)binData_[b] / (double)sampleCount_;
        result -= p == 0.0 ? 0.0 : p * Math.Log(p, 2.0);
      }

      return result;
    }

    public ushort GetBin(int histogramHandle, int bin)
    {
      return binData_[histogramHandle + bin];
    }

    public void Increment(int handle, int bin)
    {
      binData_[handle + bin]++;
    }

    public int FindTallestBinIndex(int handle)
    {
      int maxCount = binData_[handle + 0];
      int tallestBinIndex = 0;

      for (int i = 1; i < nBins_; i++)
      {
        if (binData_[handle + i] > maxCount)
        {
          maxCount = binData_[handle + i];
          tallestBinIndex = i;
        }
      }

      return tallestBinIndex;
    }

    public void Clear(int handle)
    {
      for (int i = handle; i < handle + nBins_; i++)
        binData_[i] = 0;
    }

    public void Aggregate(int destinationHandle, int sourceHandle)
    {
      for (int i = 0; i < nBins_; i++)
      {
        binData_[destinationHandle + i] += binData_[sourceHandle + i];
      }
    }
  }

  [Serializable]
  struct HistogramAggregator : IStatisticsAggregator<HistogramAggregator>
  {
    // If this histogram were of known size, we might in principle prefer to 
    // store the bin counts locally within this struct rather than on a heap
    // allocated array. However, unlike C++, this is surprisingly hard to
    // accomplish in C# because of the lack of pointers.

    int sampleCount_;

    int dataHandle_;

    public double Entropy(HistogramData data)
    {
      //if (sampleCount_ == 0)
      //  return 0.0;

      //double result = 0.0;
      //for (int b = 0; b < bins_.Length; b++)
      //{
      //  double p = (double)bins_[b] / (double)sampleCount_;
      //  result -= p == 0.0 ? 0.0 : p * Math.Log(p, 2.0);
      //}

      //return result;

      return data.Entropy(dataHandle_, sampleCount_);
    }

    public HistogramAggregator(int nClasses, HistogramData data)
    {
      // bins_ = new int[nClasses];
      dataHandle_ = data.GetMemory();

      sampleCount_ = 0;
    }

    public float GetProbability(int classIndex, HistogramData data)
    {
      // return (float)(bins_[classIndex]) / sampleCount_;
      return (float)(data.GetBin(dataHandle_, classIndex) / sampleCount_);
    }

    //public int BinCount {
    //  get { return bins_.Length; }
    //}

    public int SampleCount { get { return sampleCount_; } }

    //public int FindTallestBinIndex()
    //{
    //  int maxCount = bins_[0];
    //  int tallestBinIndex = 0;

    //  for (int i = 1; i < bins_.Length; i++)
    //  {
    //    if (bins_[i] > maxCount)
    //    {
    //      maxCount = bins_[i];
    //      tallestBinIndex = i;
    //    }
    //  }

    //  return tallestBinIndex;
    //}

    #region IStatisticsAggregator implementation
    public void Clear(Object userData)
    {
      HistogramData histogramData = (HistogramData)(userData);
      histogramData.Clear(dataHandle_);
      //for (int b = 0; b < bins_.Length; b++)
      //  bins_[b] = 0;

      sampleCount_ = 0;
    }

    public void Aggregate(IDataPointCollection data, int index, Object userData)
    {
      DataPointCollection concreteData = (DataPointCollection)(data);
      HistogramData histogramData = (HistogramData)(userData);
      histogramData.Increment(dataHandle_, concreteData.GetIntegerLabel((int)index));
      // bins_[concreteData.GetIntegerLabel((int)index)]++;
      sampleCount_ += 1;
    }

    public void Aggregate(HistogramAggregator aggregator, Object userData)
    {
      // System.Diagnostics.Debug.Assert(aggregator.bins_.Length == bins_.Length);

      HistogramData histogramData = (HistogramData)(userData);

      histogramData.Aggregate(dataHandle_, aggregator.dataHandle_);

      //for (int b = 0; b < bins_.Length; b++)
      //  bins_[b] += aggregator.bins_[b];

      sampleCount_ += aggregator.sampleCount_;
    }

    // TODO: this
    public void Clone(HistogramAggregator source)
    {

    }

    // But there's still an issuie.

    // THe histogram data used during training is different from that stored in the tree.
    
    // I don't want to store any of hte data used during training.

    // I could implement ITrainingContext.GetStatisticsAggregatorTemporary() and
    // ITrainingContext.GetStatisticsAggregatorTree()?

    // Or I could implement ITrainingContext.FreeStatisticsAggregator()?

    // Or, within InitializeNode, when I take a Clone, I could do some magic...
    // 

    public HistogramAggregator DeepClone()
    {
      HistogramAggregator result = new HistogramAggregator((int)(bins_.Length));

      for (int b = 0; b < bins_.Length; b++)
        result.bins_[b] = bins_[b];

      result.sampleCount_ = sampleCount_;

      return result;
    }

    #endregion
  }

  [Serializable]
  struct SemiSupervisedClassificationStatisticsAggregator : IStatisticsAggregator<SemiSupervisedClassificationStatisticsAggregator>
  {
    int nClasses_;
    double a_, b_;

    public GaussianAggregator2d GaussianAggregator2d;
    public HistogramAggregator HistogramAggregator;

    public SemiSupervisedClassificationStatisticsAggregator(int nClasses, double a, double b)
    {
      nClasses_ = nClasses;
      a_ = a;
      b_ = b;

      GaussianAggregator2d = new GaussianAggregator2d(a, b);
      HistogramAggregator = new HistogramAggregator(nClasses);
    }

    #region IStatisticsAggregator implementation

    public void Clear(Object userData)
    {
      GaussianAggregator2d.Clear(userData);
      HistogramAggregator.Clear(userData);
    }

    public void Aggregate(IDataPointCollection data, int index, Object userData)
    {
      DataPointCollection concreteData = (DataPointCollection)(data);

      // Always aggregate density statistics
      GaussianAggregator2d.Aggregate(data, index, userData);

      // Only aggregate histogram statistics for those data points that have class labels
      if (concreteData.GetIntegerLabel((int)(index)) != DataPointCollection.UnknownClassLabel)
        HistogramAggregator.Aggregate(data, index, userData);
    }

    public void Aggregate(SemiSupervisedClassificationStatisticsAggregator aggregator, Object userData)
    {
      GaussianAggregator2d.Aggregate(aggregator.GaussianAggregator2d, userData);
      HistogramAggregator.Aggregate(aggregator.HistogramAggregator, userData);
    }

    public SemiSupervisedClassificationStatisticsAggregator DeepClone()
    {
      SemiSupervisedClassificationStatisticsAggregator clone = new SemiSupervisedClassificationStatisticsAggregator(nClasses_, a_, b_);

      clone.GaussianAggregator2d = GaussianAggregator2d.DeepClone();
      clone.HistogramAggregator = HistogramAggregator.DeepClone();

      return clone;
    }

    #endregion
  }

  [Serializable]
  class GaussianPdf2d
  {
    private double mean_x_, mean_y_;
    private double Sigma_11_, Sigma_12_, Sigma_22_; // symmetric 2x2 covariance matrix
    private double inv_Sigma_11_, inv_Sigma_12_, inv_Sigma_22_; // symmetric 2x2 inverse covariance matrix
    private double det_Sigma_;
    private double log_det_Sigma_;

    public GaussianPdf2d(double mu_x, double mu_y, double Sigma_11, double Sigma_12, double Sigma_22)
    {
      mean_x_ = mu_x;
      mean_y_ = mu_y;

      Sigma_11_ = Sigma_11;
      Sigma_12_ = Sigma_12;
      Sigma_22_ = Sigma_22;

      det_Sigma_ = Sigma_11 * Sigma_22 - Sigma_12 * Sigma_12;

      if (det_Sigma_ < 0.0)
        throw new ArgumentException("Gaussian covaraince matrix must have determinant>0.0.");

      log_det_Sigma_ = Math.Log(det_Sigma_);

      inv_Sigma_11_ = Sigma_22 / det_Sigma_;
      inv_Sigma_22_ = Sigma_11 / det_Sigma_;
      inv_Sigma_12_ = -Sigma_12 / det_Sigma_;
    }

    public double MeanX
    {
      get
      {
        return mean_x_;
      }
    }

    public double MeanY
    {
      get
      {
        return mean_y_;
      }
    }

    public double VarianceX
    {
      get
      {
        return Sigma_11_;
      }
    }

    public double VarianceY
    {
      get
      {
        return Sigma_22_;
      }
    }

    public double CovarianceXY
    {
      get
      {
        return Sigma_12_;
      }
    }

    public double GetProbability(float x, float y)
    {
      double x_ = x - mean_x_;
      double y_ = y - mean_y_;

      return Math.Pow(2.0 * Math.PI, -1.0) * Math.Pow(det_Sigma_, -0.5) * Math.Exp(
          -0.5 * x_ * (inv_Sigma_11_ * x_ + inv_Sigma_12_ * y_) - 0.5 * y_ * (inv_Sigma_12_ * x_ + inv_Sigma_22_ * y_));
    }

    public double GetNegativeLogProbability(float x, float y)
    {
      double x_ = x - mean_x_;
      double y_ = y - mean_y_;

      double result = 0.5 * log_det_Sigma_ + 0.5 * (x_ * (inv_Sigma_11_ * x_ + inv_Sigma_12_ * y_) + y_ * (inv_Sigma_12_ * x_ + inv_Sigma_22_ * y_));

      return result;
    }

    public double Entropy()
    {
      double determinant = Sigma_11_ * Sigma_22_ - Sigma_12_ * Sigma_12_;

      if (determinant <= 0.0)
      {
        // If we used a sensible prior, this wouldn't happen. So the user can test
        // without a prior, we fail gracefully.
        return Double.PositiveInfinity;
      }

      return 0.5 * Math.Log(Math.Pow(2.0 * Math.PI * Math.E, 2.0) * determinant);
    }
  }

  [Serializable]
  struct GaussianAggregator2d : IStatisticsAggregator<GaussianAggregator2d>
  {
    private int sampleCount_;

    private double sx_, sy_;    // sum
    private double sxx_, syy_;  // sum squares
    private double sxy_;        // sum products

    // A little wasteful to store this information per node, but simpler...
    private double a_, b_;      // hyperparameters of prior

    public GaussianAggregator2d(double a, double b)
    {
      System.Diagnostics.Debug.Assert(a >= 0.0 && b >= 0.0, "Hyperparameters a and b must be greater than or equal to zero.");

      sx_ = 0.0; sy_ = 0.0;
      sxx_ = 0.0; syy_ = 0.0;
      sxy_ = 0.0;
      sampleCount_ = 0;

      a_ = a;
      b_ = b;

      // The prior should gaurantee non-degeneracy but the caller can
      // deactivate it (by setting hyperparameter a to 0.0). Therefore
      // we have to recover from degenerate covariance matrices.
      if (a_ < 0.001)
        a_ = 0.001;
      if (b_ < 1)
        b_ = 1.0;
    }

    public GaussianPdf2d GetPdf()
    {
      System.Diagnostics.Debug.Assert(sampleCount_ > 0, "At least one sample required for PDF computation.");

      // Compute maximum likelihood mean and covariance matrix
      double mx = sx_ / sampleCount_;
      double my = sy_ / sampleCount_;
      double vxx = sxx_ / sampleCount_ - (sx_ * sx_) / (sampleCount_ * sampleCount_);
      double vyy = syy_ / sampleCount_ - (sy_ * sy_) / (sampleCount_ * sampleCount_);
      double vxy = sxy_ / sampleCount_ - (sx_ * sy_) / (sampleCount_ * sampleCount_);

      // simple adaptation using prior
      double alpha = sampleCount_ / (sampleCount_ + a_);
      vxx = alpha * vxx + (1 - alpha) * b_;
      vyy = alpha * vyy + (1 - alpha) * b_;
      vxy = alpha * vxy;

      System.Diagnostics.Debug.Assert(vxx * vyy - vxy * vxy > 0, "Degenerate Gaussian.");

      return new GaussianPdf2d(mx, my, vxx, vxy, vyy);
    }

    public int SampleCount { get { return sampleCount_; } }

    #region IStatisticsAggregator implementation
    public Object UserData { get { return null; } }

    public void Clear(Object userData)
    {
      sx_ = 0.0; sy_ = 0.0;
      sxx_ = 0.0; syy_ = 0.0;
      sxy_ = 0.0;
      sampleCount_ = 0;
    }

    public void Aggregate(IDataPointCollection data, int index, Object userData)
    {
      DataPointCollection concreteData = (DataPointCollection)(data);

      sx_ += concreteData.GetDataPoint((int)index)[0];
      sy_ += concreteData.GetDataPoint((int)index)[1];

      sxx_ += Math.Pow(concreteData.GetDataPoint((int)index)[0], 2.0);
      syy_ += Math.Pow(concreteData.GetDataPoint((int)index)[1], 2.0);

      sxy_ += concreteData.GetDataPoint((int)index)[0] * concreteData.GetDataPoint((int)index)[1];

      sampleCount_ += 1;
    }

    public void Aggregate(GaussianAggregator2d aggregator, Object userData)
    {
      sx_ += aggregator.sx_;
      sy_ += aggregator.sy_;

      sxx_ += aggregator.sxx_;
      syy_ += aggregator.syy_;

      sxy_ += aggregator.sxy_;

      sampleCount_ += aggregator.sampleCount_;
    }

    public GaussianAggregator2d DeepClone()
    {
      return this;
    }

    #endregion
  }

  [Serializable]
  struct LinearFitAggregator1d : IStatisticsAggregator<LinearFitAggregator1d>
  {
    private int sampleCount_;

    // Good reference for Bayesian linear regression: http://see.stanford.edu/materials/aimlcs229/cs229-gp.pdf

    private double XT_X_11_, XT_X_12_;
    private double XT_X_21_, XT_X_22_;

    private double XT_Y_1_;
    private double XT_Y_2_;

    private double Y2_;

    public double Entropy()
    {
      if (sampleCount_ < 3)
        return Double.PositiveInfinity;

      double determinant = XT_X_11_ * XT_X_22_ - XT_X_12_ * XT_X_12_;

      if (determinant == 0.0)
        return Double.PositiveInfinity;

      return 0.5 * Math.Log(Math.Pow(2.0 * Math.PI * Math.E, 2.0) * determinant);
    }

    public double GetProbability(float x, float y)
    {
      // http://mathworld.wolfram.com/CorrelationCoefficient.html
      double mean_x = XT_X_12_ / sampleCount_;
      double ss_x = XT_X_11_ - sampleCount_ * mean_x * mean_x;

      double mean_y = XT_Y_2_ / sampleCount_;
      double ss_y = Y2_ - sampleCount_ * mean_y * mean_y;

      double ss_xy = XT_Y_1_ - sampleCount_ * mean_y * mean_x;

      double r2 = (ss_xy * ss_xy) / (ss_x * ss_y);
      double sigma_2 = ss_y * (1.0 - r2) / sampleCount_;

      // http://see.stanford.edu/materials/aimlcs229/cs229-gp.pdf

      double determinant = XT_X_11_ * XT_X_22_ - XT_X_12_ * XT_X_12_;

      double A_11 = sigma_2 * XT_X_22_ / determinant, A_12 = -sigma_2 * XT_X_12_ / determinant;
      double A_21 = -sigma_2 * XT_X_12_ / determinant, A_22 = sigma_2 * XT_X_11_ / determinant;

      double mean = (x * (A_11 * XT_Y_1_ + A_12 * XT_Y_2_)) / (sigma_2) + (A_21 * XT_Y_1_ + A_22 * XT_Y_2_) / (sigma_2);
      double variance = x * (A_11 * x + A_12) + (A_21 * x + A_22) + sigma_2;

      return Math.Pow(2.0 * Math.PI, -0.5) * Math.Pow(variance, -0.5) * Math.Exp(-0.5 * (y - mean) * (y - mean) / (variance));
    }

    public int SampleCount
    {
      get { return sampleCount_; }
    }

    #region IStatisticsAggregator implementation
    public void Clear(Object userData)
    {
      XT_X_11_ = 0.0; XT_X_12_ = 0.0;
      XT_X_21_ = 0.0; XT_X_22_ = 0.0;

      XT_Y_1_ = 0.0;
      XT_Y_2_ = 0.0;

      Y2_ = 0.0;

      sampleCount_ = 0;
    }

    public void Aggregate(IDataPointCollection data, int index, Object userData)
    {
      DataPointCollection concreteData = (DataPointCollection)(data);

      float[] datum = concreteData.GetDataPoint((int)index);
      float target = concreteData.GetTarget((int)index);

      XT_X_11_ += datum[0] * datum[0];
      XT_X_12_ += datum[0];
      XT_X_21_ += datum[0];
      XT_X_22_ += 1.0;

      XT_Y_1_ += datum[0] * target;
      XT_Y_2_ += target;

      Y2_ += target * target;

      sampleCount_ += 1;
    }

    public void Aggregate(LinearFitAggregator1d aggregator, Object userData)
    {
      LinearFitAggregator1d linearFitAggregator = (LinearFitAggregator1d)(aggregator);

      XT_X_11_ += linearFitAggregator.XT_X_11_; XT_X_12_ += linearFitAggregator.XT_X_12_;
      XT_X_21_ += linearFitAggregator.XT_X_21_; XT_X_22_ += linearFitAggregator.XT_X_22_;

      XT_Y_1_ += linearFitAggregator.XT_Y_1_;
      XT_Y_2_ += linearFitAggregator.XT_Y_2_;

      Y2_ += linearFitAggregator.Y2_;

      sampleCount_ += linearFitAggregator.sampleCount_;
    }

    public LinearFitAggregator1d DeepClone()
    {
      LinearFitAggregator1d result = new LinearFitAggregator1d();

      result.XT_X_11_ = XT_X_11_; result.XT_X_12_ = XT_X_12_;
      result.XT_X_21_ = XT_X_21_; result.XT_X_22_ = XT_X_22_;

      result.XT_Y_1_ = XT_Y_1_;
      result.XT_Y_2_ = XT_Y_2_;

      result.Y2_ = Y2_;

      result.sampleCount_ = sampleCount_;

      return result;
    }

    #endregion
  }

}
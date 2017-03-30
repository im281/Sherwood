using System;
using System.Drawing;
using System.IO;

namespace MicrosoftResearch.Cambridge.Sherwood
{
  class CumulativeNormalDistribution1d
  {
    // Please see:
    // Options, Hull, J. C., "Futures, & Other Derivatives", 5th Edition, Ch 12, pp. 248,
    // Prentice Hall, New Jersey.

    static double a1 = 0.319381530;
    static double a2 = -0.356563782;
    static double a3 = 1.781477937;
    static double a4 = -1.821255978;
    static double a5 = 1.330274429;

    static double gamma = 0.2316419;

    static double s = 1.0 / Math.Sqrt(2.0 * Math.PI);

    public static double N(double x)
    {
      double a = Math.Abs(x);
      double k = 1.0 / (1.0 + a * gamma);

      double N_ = s * Math.Exp(-0.5 * x * x);

      double sum = ((((a5 * k + a4) * k + a3) * k + a2) * k + a1) * k;

      double result = 1.0 - N_ * sum;

      if (x < 0.0)
        result = 1.0 - result;

      return result;
    }

    public static void SelfTest()
    {
      // 'Ground truth' obtained using Excel's NORMDIST() function.
      if (Math.Abs(N(0.0) - 0.50) > 1E-5)
        throw new Exception("Failed.");

      if (Math.Abs(N(-1.0) - 0.158655254) > 1E-5)
        throw new Exception("Failed.");

      if (Math.Abs(N(1.0) - 0.841344746) > 1E-5)
        throw new Exception("Failed.");

      if (Math.Abs(N(-2.0) - 0.022750132) > 1E-5)
        throw new Exception("Failed.");

      if (Math.Abs(N(2.0) - 0.977249868) > 1E-5)
        throw new Exception("Failed.");

      if (Math.Abs(N(0.330) - 0.629300019) > 1E-5)
        throw new Exception("Failed.");

      if (Math.Abs(N(-0.330) - 0.370699981) > 1E-5)
        throw new Exception("Failed.");

      if (Math.Abs(N(6.0) - 1.0) > 1E-5)
        throw new Exception("Failed.");

      if (Math.Abs(N(-6.0) - 0.0) > 1E-5)
        throw new Exception("Failed.");

      if (Math.Abs(N(10000.0) - 1.0) > 1E-5)
        throw new Exception("Failed.");

      if (Math.Abs(N(-10000.0) - 0.0) > 1E-5)
        throw new Exception("Failed.");

      if (Math.Abs(N(double.NegativeInfinity) - 0.0) > 1E-5)
        throw new Exception("Failed.");

      if (Math.Abs(N(double.PositiveInfinity) - 1.0) > 1E-5)
        throw new Exception("Failed.");
    }
  }

  class CumulativeNormalDistribution2d
  {
    // Please see:
    // Options, Hull, J. C., "Futures, & Other Derivatives", 5th Edition, Appendix 12C, pp. 266,
    // Prentice Hall, New Jersey;

    static double f(double x, double y, double a_, double b_, double rho)
    {
      double result = a_ * (2 * x - a_) + b_ * (2 * y - b_) + 2 * rho * (x - a_) * (y - b_);
      return Math.Exp(result);
    }

    static double[] A = new double[4] { 0.3253030, 0.4211071, 0.1334425, 0.006374323 };
    static double[] B = new double[4] { 0.1337764, 0.6243247, 1.3425378, 2.2626645 };

    static double N(double x)
    {
      return CumulativeNormalDistribution1d.N(x);
    }

    public static double M(double a, double b, double rho)
    {
      if (a > 100.0)
        a = 100.0;
      if (a < -100.0)
        a = -100.0;
      if (b > 100.0)
        b = 100.0;
      if (b < -100.0)
        b = -100.0;

      if (a <= 0.0 && b <= 0.0 && rho <= 0.0)
      {
        double a_ = a / Math.Sqrt(2.0 * (1.0 - rho * rho));
        double b_ = b / Math.Sqrt(2.0 * (1.0 - rho * rho));

        double sum = 0.0;
        for (int i = 0; i < 4; i++)
          for (int j = 0; j < 4; j++)
            sum += A[i] * A[j] * f(B[i], B[j], a_, b_, rho);
        sum = sum * Math.Sqrt(1.0 - rho * rho) / Math.PI;
        return sum;
      }
      else if (a * b * rho <= 0.0)
      {
        if (a <= 0.0 && b >= 0.0 && rho >= 0.0)
          return N(a) - M(a, -b, -rho);
        else if (a >= 0.0 && b <= 0.0 && rho >= 0.0)
          return N(b) - M(-a, b, -rho);
        else if (a >= 0.0 && b >= 0.0 && rho <= 0.0)
          return N(a) + N(b) - 1.0 + M(-a, -b, rho);
      }
      else if (a * b * rho >= 0.0)
      {
        double denominator = Math.Sqrt(a * a - 2.0 * rho * a * b + b * b);
        double rho1 = ((rho * a - b) * Math.Sign(a)) / denominator;
        double rho2 = ((rho * b - a) * Math.Sign(b)) / denominator;
        double delta = (1.0 - Math.Sign(a) * Math.Sign(b)) / 4.0;
        return M(a, 0.0, rho1) + M(b, 0.0, rho2) - delta;
      }
      throw new Exception("Invalid input for computation of bivariate normal CDF.");
    }

    static public void SelfTest()
    {
      if (Math.Abs(M(0.0, 0.0, 0.0) - 0.25) > 1E-5)
        throw new Exception("Failed.");

      if (Math.Abs(M(0.0, 0.0, -0.5) - 0.16666) > 1E-5)
        throw new Exception("Failed.");

      if (Math.Abs(M(0.0, 0.0, 0.5) - 0.3333333) > 1E-5)
        throw new Exception("Failed.");

      if (Math.Abs(M(6.0, 0.0, 0.0) - 0.5) > 1E-5)
        throw new Exception("Failed.");

      if (Math.Abs(M(-6.0, 0.0, 0.0) - 0.0) > 1E-5)
        throw new Exception("Failed.");

      if (Math.Abs(M(0.0, 6.0, 0.0) - 0.5) > 1E-5)
        throw new Exception("Failed.");

      if (Math.Abs(M(0.0, -6.0, 0.0) - 0.0) > 1E-5)
        throw new Exception("Failed.");

      if (Math.Abs(M(6.0, 6.0, 0.0) - 1.0) > 1E-5)
        throw new Exception("Failed.");

      if (Math.Abs(M(double.NegativeInfinity, double.NegativeInfinity, 0.5) - 0.0) > 1E-5)
        throw new Exception("Failed.");

      if (Math.Abs(M(double.PositiveInfinity, double.PositiveInfinity, 0.5) - 1.0) > 1E-5)
        throw new Exception("Failed.");

      if (Math.Abs(M(double.NegativeInfinity, double.PositiveInfinity, 0.5) - 0.0) > 1E-5)
        throw new Exception("Failed.");

      if (Math.Abs(M(double.PositiveInfinity, double.NegativeInfinity, 0.5) - 0.0) > 1E-5)
        throw new Exception("Failed.");
    }
  }
}

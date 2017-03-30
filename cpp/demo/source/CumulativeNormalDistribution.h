#pragma once

#include <cmath>

#include <stdexcept>
#include <limits>

namespace MicrosoftResearch { namespace Cambridge { namespace Sherwood
{
  template <typename T> int sign(T val)
  {
    return (T(0) < val) - (val < T(0));
  }

  class CumulativeNormalDistribution1d
  {
    // Please see:
    // Options, Hull, J. C., "Futures, & Other Derivatives", 5th Edition, Ch 12, pp. 248,
    // Prentice Hall, New Jersey.

    static const double a1, a2, a3, a4, a5;
    static const double gamma;
    static const double s;
  public:
    static const double pi;

  public:
    static double N(double x)
    {
      double a = std::abs(x);
      double k = 1.0 / (1.0 + a * gamma);

      double N_ = s * exp(-0.5 * x * x);

      double sum = ((((a5 * k + a4) * k + a3) * k + a2) * k + a1) * k;

      double result = 1.0 - N_ * sum;

      if (x < 0.0)
        result = 1.0 - result;

      return result;
    }

    static void SelfTest()
    {
      // 'Ground truth' obtained using Excel's NORMDIST() function.
      if (std::abs(N(0.0) - 0.50) > 1E-5)
        throw std::runtime_error("Failed.");

      if (std::abs(N(-1.0) - 0.158655254) > 1E-5)
        throw std::runtime_error("Failed.");

      if (std::abs(N(1.0) - 0.841344746) > 1E-5)
        throw std::runtime_error("Failed.");

      if (std::abs(N(-2.0) - 0.022750132) > 1E-5)
        throw std::runtime_error("Failed.");

      if (std::abs(N(2.0) - 0.977249868) > 1E-5)
        throw std::runtime_error("Failed.");

      if (std::abs(N(0.330) - 0.629300019) > 1E-5)
        throw std::runtime_error("Failed.");

      if (std::abs(N(-0.330) - 0.370699981) > 1E-5)
        throw std::runtime_error("Failed.");

      if (std::abs(N(6.0) - 1.0) > 1E-5)
        throw std::runtime_error("Failed.");

      if (std::abs(N(-6.0) - 0.0) > 1E-5)
        throw std::runtime_error("Failed.");

      if (std::abs(N(10000.0) - 1.0) > 1E-5)
        throw std::runtime_error("Failed.");

      if (std::abs(N(-10000.0) - 0.0) > 1E-5)
        throw std::runtime_error("Failed.");

      if (std::abs(N(-std::numeric_limits<double>::infinity()) - 0.0) > 1E-5)
        throw std::runtime_error("Failed.");

      if (std::abs(N(std::numeric_limits<double>::infinity()) - 1.0) > 1E-5)
        throw std::runtime_error("Failed.");
    }
  };

  class CumulativeNormalDistribution2d
  {
  public:
    // Please see:
    // Options, Hull, J. C., "Futures, & Other Derivatives", 5th Edition, Appendix 12C, pp. 266,
    // Prentice Hall, New Jersey;

    static double f(double x, double y, double a_, double b_, double rho)
    {
      double result = a_ * (2 * x - a_) + b_ * (2 * y - b_) + 2 * rho * (x - a_) * (y - b_);
      return exp(result);
    }

    static const double A[4]; // = { 0.3253030, 0.4211071, 0.1334425, 0.006374323 };
    static const double B[4]; // = { 0.1337764, 0.6243247, 1.3425378, 2.2626645 };

    static double N(double x)
    {
      return CumulativeNormalDistribution1d::N(x);
    }

    static double M(double a, double b, double rho)
    {
      if (a>100.0)
        a = 100.0;
      if (a<-100.0)
        a = -100.0;
      if (b>100.0)
        b = 100.0;
      if (b<-100.0)
        b = -100.0;

      if (a <= 0.0 && b <= 0.0 && rho <= 0.0)
      {
        double a_ = a / sqrt(2.0 * (1.0 - rho * rho));
        double b_ = b / sqrt(2.0 * (1.0 - rho * rho));

        double sum = 0.0;
        for (int i = 0; i < 4; i++)
          for (int j = 0; j < 4; j++)
            sum += A[i] * A[j] * f(B[i], B[j], a_, b_, rho);
        sum = sum * sqrt(1.0 - rho * rho) / CumulativeNormalDistribution1d::pi;
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
        double denominator = sqrt(a * a - 2.0 * rho * a * b + b * b);
        double rho1 = ((rho * a - b) * sign(a)) / denominator;
        double rho2 = ((rho * b - a) * sign(b)) / denominator;
        double delta = (1.0 - sign(a) * sign(b)) / 4.0;
        return M(a, 0.0, rho1) + M(b, 0.0, rho2) - delta;
      }
      throw std::runtime_error("Invalid input for computation of bivariate normal CDF.");
    }

    static void SelfTest()
    {
      if (std::abs(M(0.0, 0.0, 0.0) - 0.25) > 1E-5)
        throw std::runtime_error("Failed.");

      if (std::abs(M(0.0, 0.0, -0.5) - 0.16666) > 1E-5)
        throw std::runtime_error("Failed.");

      if (std::abs(M(0.0, 0.0, 0.5) - 0.3333333) > 1E-5)
        throw std::runtime_error("Failed.");

      if (std::abs(M(6.0, 0.0, 0.0) - 0.5) > 1E-5)
        throw std::runtime_error("Failed.");

      if (std::abs(M(-6.0, 0.0, 0.0) - 0.0) > 1E-5)
        throw std::runtime_error("Failed.");

      if (std::abs(M(0.0, 6.0, 0.0) - 0.5) > 1E-5)
        throw std::runtime_error("Failed.");

      if (std::abs(M(0.0, -6.0, 0.0) - 0.0) > 1E-5)
        throw std::runtime_error("Failed.");

      if (std::abs(M(6.0, 6.0, 0.0) - 1.0) > 1E-5)
        throw std::runtime_error("Failed.");

      if (std::abs(M(-std::numeric_limits<double>::infinity(), -std::numeric_limits<double>::infinity(), 0.5) - 0.0) > 1E-5)
        throw std::runtime_error("Failed.");

      if (std::abs(M(std::numeric_limits<double>::infinity(), std::numeric_limits<double>::infinity(), 0.5) - 1.0) > 1E-5)
        throw std::runtime_error("Failed.");

      if (std::abs(M(-std::numeric_limits<double>::infinity(), std::numeric_limits<double>::infinity(), 0.5) - 0.0) > 1E-5)
        throw std::runtime_error("Failed.");

      if (std::abs(M(std::numeric_limits<double>::infinity(), -std::numeric_limits<double>::infinity(), 0.5) - 0.0) > 1E-5)
        throw std::runtime_error("Failed.");
    }
  };
} } }

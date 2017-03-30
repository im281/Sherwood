#include "CommandLineParser.h"

namespace MicrosoftResearch { namespace Cambridge { namespace Sherwood
{
  std::string toLower(const std::string& i)
  {
    std::string s = i;

    // NB Weird conversion clause around std::tolower necessary to persuade
    // gcc to disambiguate between two versions of std::toupper (in <cctype>
    // and <locale>.
    std::transform(s.begin(), s.end(), s.begin(), (int (*)(int))std::tolower);

    return s;
  }

  std::string toUpper(const std::string& i)
  {
    std::string s = i;

    // NB Weird conversion clause around std::tolower necessary to persuade
    // gcc to disambiguate between two versions of std::toupper (in <cctype>
    // and <locale>.
    std::transform(s.begin(), s.end(), s.begin(), (int (*)(int))std::toupper);

    return s;
  }

  std::string padRight(const std::string& s, std::string::size_type n)
  {
    std::string result = s;
    while(result.length()<n)
      result += " ";

    return result;
  }

  int convertToInt(const std::string& s)
  {
    int x;
    std::stringstream ss(s);
    ss >> x;

    if(ss.bad())
      throw std::runtime_error("Failed to interpret as integer.");

    return x;
  }

  float convertToSingle(const std::string& s)
  {
    float x;
    std::stringstream ss(s);
    ss >> x;

    if(ss.bad())
      throw std::runtime_error("Failed to interpret as integer.");

    return x;
  }

} } }

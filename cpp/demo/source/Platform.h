#pragma once

// This file declares functions for retrieving the executable path and obtaining
// a directory listing. These need to be implemented in a platform-specific way for
// Windows and Linux operatings systems.

#include <string>
#include <vector>

namespace MicrosoftResearch { namespace Cambridge { namespace Sherwood
{
  std::string GetExecutablePath();

  void GetDirectoryListing(const std::string& path, std::vector<std::string>& filenames, const std::string& extension);
} } }

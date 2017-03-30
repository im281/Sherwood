#include "Platform.h"

#include <string.h>

#include <stdexcept>
#include <iostream>

#ifdef WIN32
#include <Windows.h>
#else
#include <sys/types.h>
#include <dirent.h>
#include <unistd.h>
#endif

namespace MicrosoftResearch { namespace Cambridge { namespace Sherwood
{
  void GetDirectoryListing(const std::string& path, std::vector<std::string>& filenames, const std::string& extension)
  {
    filenames.clear();

#ifdef WIN32
    WIN32_FIND_DATA fdFile;
    HANDLE hFind = NULL;

    std::string wildcardPath = path + "/*" + extension;

    if((hFind = FindFirstFile(wildcardPath.c_str(), &fdFile)) == INVALID_HANDLE_VALUE)
      throw std::runtime_error("Failed to obtain directory listing.");

    try
    {
      do
      {   
        std::string name = fdFile.cFileName;
        if(name=="." || name=="..")
          continue;
        filenames.push_back(name);
      } while(FindNextFile(hFind, &fdFile)); //Find the next file.
    }
    catch(...)
    {
      FindClose(hFind);
      throw;
    }

    FindClose(hFind);
#else
    DIR *dp;
    struct dirent *dirp;
    if((dp  = opendir(path.c_str())) == NULL)
      throw std::runtime_error("Failed to obtain directory listing.");

    try
    {
      while ((dirp = readdir(dp)) != NULL)
      {
        std::string name = dirp->d_name;
        if(name=="." || name=="..")
          continue;
        if(extension!="" && name.substr(name.size()-4,4)!=extension)
          continue;
        filenames.push_back(name);
      }
    }
    catch(...)
    {
      closedir(dp);
      throw;
    }

    closedir(dp);   
#endif
  }

  std::string GetExecutablePath()
  {
#ifdef WIN32
    HMODULE hModule = GetModuleHandle(NULL);
    if(hModule==NULL)
      throw std::runtime_error("Failed to obtain module handle.");
    CHAR path[MAX_PATH];
    if(GetModuleFileName(hModule, path, MAX_PATH)==0)
      throw std::runtime_error("Failed to obtain excutable path.");

    std::string executablePath(path);

    std::string::size_type s = executablePath.find_last_of('\\');
    if(s==std::string::npos)
      throw std::runtime_error("Failed to parse path returned by GetModuleFileName().");

    executablePath = executablePath.substr(0, s);
    return executablePath;
#else
    char buff[1024];
    ssize_t len = ::readlink("/proc/self/exe", buff, sizeof(buff)-1);
    if (len == -1)
      throw std::runtime_error("Failed to retrieve executable path.");

    buff[len] = '\0';

    std::string executablePath(buff);

    std::string::size_type s = executablePath.find_last_of('/');
    if(s==std::string::npos)
      throw std::runtime_error("Failed to parse path returned by GetModuleFileName().");

    executablePath = executablePath.substr(0, s);
    return executablePath;
#endif
  }
} } }

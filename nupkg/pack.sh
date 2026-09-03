#!/bin/bash

cd "$(dirname "$0")"

source='https://mes-nexus.wecharmer.com/repository/nuget-hosted'
api_key='ad571298-4c13-34fd-a3e1-0b6632b0476f'
package_dir='../src/bin'

echo -e "\n============ Build Solution (Release) ============\n"
echo "nupkg will be generated to ${package_dir} via GeneratePackageOnBuild"
dotnet build ../BeniceSoft.Abp.sln -c Release
if [ $? -ne 0 ]; then
  exit 1
fi

echo -e "\n============ Push Packages from src/bin ============\n"
shopt -s nullglob
packages=("${package_dir}"/*.nupkg)
if [ ${#packages[@]} -eq 0 ]; then
  echo "No .nupkg found in ${package_dir}, please build the solution first."
  exit 1
fi

for file in "${packages[@]}"; do
  filename="$(basename "$file")"
  if [[ "$filename" == *Sample* ]]; then
    rm -rf "$file"
    echo "package ${filename} deleted! (Sample package)"
    continue
  fi

  echo "push nuget package ${filename}"
  dotnet nuget push "$file" -s "$source" --api-key "$api_key"
  if [ $? -ne 0 ]; then
    exit 1
  fi
  echo "package ${filename} pushed!"
  rm -rf "$file"
  echo "package ${filename} deleted!"
done

echo -e "\n============ Done ============\n"

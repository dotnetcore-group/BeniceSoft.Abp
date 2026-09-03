#!/bin/bash
set -euo pipefail

cd "$(dirname "$0")"

# GitHub Packages NuGet feed (org: dotnetcore-group)
source_url='https://nuget.pkg.github.com/dotnetcore-group/index.json'
source_name='github'
package_dir='../src/bin'

# 1) Prefer env PAT  2) Else reuse password already stored for source "github"
api_key="${GH_PACKAGES_PAT:-${GITHUB_TOKEN:-}}"
if [[ -z "$api_key" ]] && command -v powershell.exe >/dev/null 2>&1; then
  api_key="$(powershell.exe -NoProfile -Command "\$c=[xml](Get-Content -Raw \"\$env:APPDATA\NuGet\NuGet.Config\"); \$n=\$c.configuration.packageSourceCredentials.github; if(\$n){ (\$n.add | Where-Object key -eq 'ClearTextPassword').value }" | tr -d '\r')"
fi

if [[ -z "${api_key:-}" ]]; then
  echo "ERROR: No GitHub Packages credentials found."
  echo "Options:"
  echo "  1) export GH_PACKAGES_PAT=ghp_xxxxxxxx   then re-run"
  echo "  2) or once: dotnet nuget add source \"$source_url\" --name $source_name --username YOUR_USER --password YOUR_PAT --store-password-in-clear-text"
  exit 1
fi

username="${GH_USERNAME:-${USER:-github}}"

echo -e "\n============ Ensure GitHub NuGet source ============\n"
if ! dotnet nuget list source | grep -qiE "${source_name}"; then
  echo "Adding source \"${source_name}\" ..."
  dotnet nuget add source "$source_url" \
    --name "$source_name" \
    --username "$username" \
    --password "$api_key" \
    --store-password-in-clear-text
else
  echo "Source \"${source_name}\" already registered (will reuse stored credentials)."
fi

echo -e "\n============ Build Solution (Release) ============\n"
echo "nupkg will be generated to ${package_dir} via GeneratePackageOnBuild"
dotnet build ../BeniceSoft.Abp.sln -c Release

echo -e "\n============ Push Packages to GitHub Packages ============\n"
shopt -s nullglob
packages=("${package_dir}"/*.nupkg)
if [ ${#packages[@]} -eq 0 ]; then
  echo "No .nupkg found in ${package_dir}, please build the solution first."
  exit 1
fi

for file in "${packages[@]}"; do
  filename="$(basename "$file")"
  if [[ "$filename" == *Sample* ]]; then
    rm -f "$file"
    echo "skip/delete ${filename} (Sample package)"
    continue
  fi
  if [[ "$filename" == *.symbols.* ]]; then
    echo "skip ${filename} (symbols)"
    continue
  fi

  echo "push nuget package ${filename}"
  dotnet nuget push "$file" --source "$source_name" --api-key "$api_key" --skip-duplicate
  echo "package ${filename} pushed!"
  rm -f "$file"
  echo "package ${filename} deleted locally!"
done

echo -e "\n============ Done ============\n"
echo "View: https://github.com/orgs/dotnetcore-group/packages"

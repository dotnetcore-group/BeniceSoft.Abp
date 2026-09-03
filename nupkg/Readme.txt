--------------------------------------------------------------------------------
步骤 1：创建 GitHub PAT（classic）——每人首次做一次
--------------------------------------------------------------------------------

浏览器打开：
  https://github.com/settings/tokens

或：头像 → Settings → Developer settings → Personal access tokens
       → Tokens (classic) → Generate new token (classic)

填写：
  Note：例如 BeniceSoft-packages
  Expiration：可按需要选择（如 No expiration / 90 days）

勾选权限（最少）：
  [x] repo
  [x] write:packages   （会自动带上 read:packages）

不要勾选无关权限

点击 Generate token，【立刻复制】保存（只显示一次）。

若组织启用了 SSO：打开 token 列表，对该 token 点 Configure SSO / Authorize，
授权给组织 dotnetcore-group。

--------------------------------------------------------------------------------
步骤 2：本机配置 NuGet 源（每人首次做一次）
--------------------------------------------------------------------------------

在 PowerShell 中执行（把用户名和 PAT 换成你自己的）：

  dotnet nuget add source "https://nuget.pkg.github.com/dotnetcore-group/index.json" `
    --name github `
    --username 你的GitHub用户名 `
    --password 你的PAT `
    --store-password-in-clear-text

验证源是否已添加：

  dotnet nuget list source

应能看到名为 github 的源，地址为：
  https://nuget.pkg.github.com/dotnetcore-group/index.json

说明：
  - 凭据会保存在本机 %APPDATA%\NuGet\NuGet.Config
  - 配置成功后，一般【不必】再设环境变量
  - 若提示源已存在，可跳过，或先执行：
      dotnet nuget remove source github
    再重新 add

--------------------------------------------------------------------------------
步骤 2（备选）：用环境变量（不推荐长期用，适合临时推送）
--------------------------------------------------------------------------------

PowerShell（仅当前窗口有效）：

  $env:GH_PACKAGES_PAT = "你的PAT"
  $env:GH_USERNAME = "你的GitHub用户名"

CMD：

  set GH_PACKAGES_PAT=你的PAT
  set GH_USERNAME=你的GitHub用户名

Linux / macOS / Git Bash：

  export GH_PACKAGES_PAT=你的PAT
  export GH_USERNAME=你的GitHub用户名

然后再执行本目录下的 pack.bat 或 pack.sh。
脚本会优先读环境变量；没有的话会尝试读取本机已保存的 github 源密码。


--------------------------------------------------------------------------------
步骤 3：修改版本号
--------------------------------------------------------------------------------

编辑仓库根目录文件：
  common.props

修改其中的 Version，例如：

  <Version>10.0.17-dev</Version>

注意：
  - GitHub Packages 【不允许】覆盖已存在的同一版本
  - 每次发布前必须升版本号
  - -dev 表示预发布；正式对外可去掉 -dev（如 10.0.17）


--------------------------------------------------------------------------------
步骤 4：打包并推送
--------------------------------------------------------------------------------

Windows（推荐）：

  cd D:\你的路径\BeniceSoft.Abp\nupkg
  pack.bat

或在资源管理器中双击 pack.bat。

Linux / macOS / Git Bash：

  cd /你的路径/BeniceSoft.Abp/nupkg
  chmod +x pack.sh
  ./pack.sh

脚本会自动：
  1) 确认/注册 github NuGet 源
  2) Release 构建整个解决方案（GeneratePackageOnBuild 生成 nupkg）
  3) 推送 src\bin 下的包到 GitHub Packages（跳过 Sample / symbols）
  4) 推送成功后删除本地对应 nupkg


--------------------------------------------------------------------------------
步骤 5：确认发布成功
--------------------------------------------------------------------------------

打开：
  https://github.com/orgs/dotnetcore-group/packages

或仓库 Packages 页，应能看到 BeniceSoft.Abp.* / BeniceSoft.Core 等包及新版本。


--------------------------------------------------------------------------------
其他项目如何引用这些包
--------------------------------------------------------------------------------

 在项目或解决方案的 nuget.config 中建议做包源映射，例如：

  <?xml version="1.0" encoding="utf-8"?>
  <configuration>
    <packageSources>
      <clear />
      <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
      <add key="github" value="https://nuget.pkg.github.com/dotnetcore-group/index.json" />
    </packageSources>
    <packageSourceMapping>
      <packageSource key="nuget.org">
        <package pattern="*" />
      </packageSource>
      <packageSource key="github">
        <package pattern="BeniceSoft.*" />
      </packageSource>
    </packageSourceMapping>
  </configuration>

3. 项目中引用：

  <PackageReference Include="BeniceSoft.Abp.Core" Version="10.0.17-dev" />

4. 还原：

  dotnet restore


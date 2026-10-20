@echo off
chcp 65001 >nul
setlocal

:: E小智 v1.0 部署脚本
:: 将编译后的 DLL + React 前端复制到 E3D 安装目录

set E3D_DIR=D:\AVEVA\Everything3D2.10
set SRC_DIR=%~dp0src

:: dwg-sidecar：DWG 几何解析的独立进程（x64 自包含，绕开 .NET 4.8 / x86 限制）
:: 找不到时部署仍继续，但 cad_import 会回退到旧 Teigha 解析器（几何覆盖不全）
set SIDECAR_DIR=E:\工作\Reactor-Work\apps\zcode-cli\packages\dwg-tools-plugin\tools\dwg-sidecar\bin\Release\net9.0\win-x64

echo ========================================
echo E小智 v1.0 部署脚本
echo ========================================
echo.
echo E3D 目录: %E3D_DIR%
echo 源目录: %SRC_DIR%
echo.

:: 检查 E3D 目录是否存在
if not exist "%E3D_DIR%" (
    echo [错误] E3D 目录不存在: %E3D_DIR%
    pause
    exit /b 1
)

:: 检查 E3D 是否正在运行
tasklist /fi "imagename eq des.exe" 2>nul | findstr /i "des.exe" >nul
if %errorlevel% equ 0 (
    echo [警告] E3D 正在运行，请先关闭 E3D！
    echo.
    pause
    exit /b 1
)

echo [1/7] 复制 E3DCopilot.Loader.dll...
copy /y "%SRC_DIR%\E3DCopilot.Loader\bin\Release\net48\E3DCopilot.Loader.dll" "%E3D_DIR%\" >nul

echo [2/7] 复制 E3DCopilot.Addin.dll...
copy /y "%SRC_DIR%\E3DCopilot.Addin\bin\Release\net48\E3DCopilot.Addin.dll" "%E3D_DIR%\" >nul

echo [3/7] 复制 E3DCopilot.Core.dll...
copy /y "%SRC_DIR%\E3DCopilot.Core\bin\Release\net48\E3DCopilot.Core.dll" "%E3D_DIR%\" >nul

echo [4/7] 复制 E3DCopilot.Tools.dll...
copy /y "%SRC_DIR%\E3DCopilot.Tools\bin\Release\net48\E3DCopilot.Tools.dll" "%E3D_DIR%\" >nul

echo [5/7] 复制 E3DCopilot.WebHost.dll...
copy /y "%SRC_DIR%\E3DCopilot.WebHost\bin\Release\net48\E3DCopilot.WebHost.dll" "%E3D_DIR%\" >nul

echo [6/7] 复制 WebView2 运行时 DLL...
echo [6.5/7] 复制内置 skills 目录...
if not exist "%E3D_DIR%\skills" mkdir "%E3D_DIR%\skills"
xcopy /y /e /i /q "%SRC_DIR%\E3DCopilot.Core\bin\Release\net48\skills\*" "%E3D_DIR%\skills\" >nul

copy /y "%SRC_DIR%\E3DCopilot.WebHost\bin\Release\net48\Microsoft.Web.WebView2.Core.dll" "%E3D_DIR%\" >nul
copy /y "%SRC_DIR%\E3DCopilot.WebHost\bin\Release\net48\Microsoft.Web.WebView2.WinForms.dll" "%E3D_DIR%\" >nul
copy /y "%SRC_DIR%\E3DCopilot.WebHost\bin\Release\net48\Microsoft.Web.WebView2.Wpf.dll" "%E3D_DIR%\" >nul 2>nul
copy /y "%SRC_DIR%\E3DCopilot.WebHost\bin\Release\net48\WebView2Loader.dll" "%E3D_DIR%\" >nul 2>nul
copy /y "%SRC_DIR%\E3DCopilot.WebHost\bin\Release\net48\WebView2Loader32.dll" "%E3D_DIR%\" >nul 2>nul

echo [6.8/7] 复制 dwg-sidecar（DWG 几何解析引擎）...
if exist "%SIDECAR_DIR%\dwg-sidecar.exe" (
    if not exist "%E3D_DIR%\tools" mkdir "%E3D_DIR%\tools"
    xcopy /y /e /i /q "%SIDECAR_DIR%\*" "%E3D_DIR%\tools\" >nul
    echo   dwg-sidecar 已部署
) else (
    echo   [警告] 未找到 dwg-sidecar: %SIDECAR_DIR%
    echo   [警告] cad_import 将回退到旧 Teigha 解析器（按图层名关键词筛选，几何覆盖不全）
)

echo [7/7] 构建并复制 React 前端 (e3d-ui)...
echo 构建新 e3d-ui 前端...
cd /d "%~dp0e3d-ui"
call npm run build >nul 2>&1
if %errorlevel% neq 0 (
    echo [警告] e3d-ui 构建失败，请检查构建错误
)

echo 复制前端到 E3D wwwroot 目录...
if not exist "%E3D_DIR%\wwwroot" mkdir "%E3D_DIR%\wwwroot"
if exist "%E3D_DIR%\wwwroot\assets" rmdir /s /q "%E3D_DIR%\wwwroot\assets"
xcopy /y /e /i /q "%~dp0src\E3DCopilot.WebHost\wwwroot\*" "%E3D_DIR%\wwwroot\" >nul

echo.
echo ========================================
echo 部署完成！
echo ========================================
echo.
echo UI 模式: 优先 WebView2+React，WebView2 不可用时自动降级 WinForms
echo 请启动 E3D 测试 E小智插件。
echo.
pause

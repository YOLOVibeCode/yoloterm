# YOLOTerm Windows Release Guide

**Version:** 1.0
**Date:** June 12, 2026
**Audience:** Release managers and developers preparing Windows distributions

---

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Certificate Acquisition](#certificate-acquisition)
4. [Local Signing Setup](#local-signing-setup)
5. [GitHub Actions Configuration](#github-actions-configuration)
6. [Creating a Release](#creating-a-release)
7. [MSIX Packaging Details](#msix-packaging-details)
8. [Winget Submission](#winget-submission)
9. [Troubleshooting](#troubleshooting)
10. [Security Notes](#security-notes)

---

## Overview

YOLOTerm for Windows uses **Authenticode signing** for executables and **MSIX packaging** for distribution. The release process is automated via GitHub Actions, but requires user-provided signing credentials.

**Two distribution channels:**
1. **MSIX package** — Store-ready, auto-updating (via Microsoft Store)
2. **Portable ZIP** — No installation, manual updates

**Supported architectures:**
- x64 (Intel/AMD 64-bit)
- ARM64 (Windows on ARM)

---

## Prerequisites

Before you can create signed releases, you need:

### Required
- ✅ **Windows machine** (for local testing) or Windows VM
- ✅ **Windows 10 SDK** (for `signtool.exe` and `makeappx.exe`)
  - Install via Visual Studio Installer → Individual Components → "Windows 10 SDK"
  - Or standalone: https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/
- ✅ **Authenticode certificate** (see [Certificate Acquisition](#certificate-acquisition))
- ✅ **GitHub repository** with Actions enabled

### Optional
- 🔹 **Azure Trusted Signing** account (alternative to certificate file)
- 🔹 **Microsoft Partner Center** account (for Store submission)

---

## Certificate Acquisition

You need a **code signing certificate** to sign Windows executables and MSIX packages. Microsoft requires **Extended Validation (EV)** certificates for new publishers in the Microsoft Store, but **Organization Validation (OV)** certificates work for direct distribution.

### Option 1: Purchase EV/OV Certificate

**Recommended providers:**
- **DigiCert** — https://www.digicert.com/signing/code-signing-certificates
- **Sectigo** — https://sectigo.com/ssl-certificates-tls/code-signing
- **GlobalSign** — https://www.globalsign.com/en/code-signing-certificate

**Pricing:** $200–$500/year (OV), $400–$800/year (EV)

**Steps:**
1. Choose **Windows Authenticode** certificate type
2. Complete business verification (1–5 business days for OV, 1–2 weeks for EV)
3. Receive certificate as `.pfx` file (protected by password)
4. Store `.pfx` securely (see [Security Notes](#security-notes))

### Option 2: Azure Trusted Signing (Cloud-based)

**Use case:** No certificate file management; Azure handles signing

**Setup:**
1. Create Azure account: https://portal.azure.com
2. Enable "Azure Trusted Signing" service (preview as of 2026)
3. Onboard your identity (EV validation required)
4. Integrate via Azure CLI or REST API

**Pros:** No certificate expiry management, better security
**Cons:** Azure dependency, slightly more complex setup

**Reference:** https://learn.microsoft.com/en-us/azure/trusted-signing/

---

## Local Signing Setup

Before configuring CI, test signing locally.

### Step 1: Locate Tools

Find `signtool.exe` and `makeappx.exe`:
```powershell
# Windows 10 SDK (example path)
$sdkPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64"
$signtool = "$sdkPath\signtool.exe"
$makeappx = "$sdkPath\makeappx.exe"

# Verify
& $signtool /?
& $makeappx /?
```

If not found, install Windows 10 SDK via Visual Studio Installer.

### Step 2: Sign Executable

```powershell
# Sign YOLOTerm.exe
& $signtool sign `
  /f "path\to\cert.pfx" `
  /p "YourCertificatePassword" `
  /tr http://timestamp.digicert.com `
  /td sha256 `
  /fd sha256 `
  "path\to\YOLOTerm.exe"

# Verify signature
& $signtool verify /pa "path\to\YOLOTerm.exe"
```

**Flags explained:**
- `/f` — Certificate file path
- `/p` — Certificate password
- `/tr` — Timestamp server (required for long-term validity)
- `/td` — Timestamp digest algorithm (SHA-256)
- `/fd` — File digest algorithm (SHA-256)
- `/pa` — Verify against default Windows policy

### Step 3: Create MSIX

```powershell
# Prepare publish directory
cd windows
dotnet publish YOLOTerm.App/YOLOTerm.App.csproj -c Release -r win-x64 -o publish/

# Copy manifest
Copy-Item Package.appxmanifest publish/

# Create MSIX
& $makeappx pack /d publish /p YOLOTerm-0.1.0-x64.msix

# Sign MSIX
& $signtool sign `
  /f "path\to\cert.pfx" `
  /p "YourCertificatePassword" `
  /fd SHA256 `
  YOLOTerm-0.1.0-x64.msix

# Verify
& $signtool verify /pa YOLOTerm-0.1.0-x64.msix
```

### Step 4: Test Install

```powershell
# Install MSIX (requires developer mode or certificate trust)
Add-AppxPackage -Path YOLOTerm-0.1.0-x64.msix

# Launch
Start-Process "yoloterm://"

# Uninstall
Get-AppxPackage YOLOVibeCode.YOLOTerm | Remove-AppxPackage
```

---

## GitHub Actions Configuration

Once local signing works, configure CI secrets.

### Step 1: Encode Certificate

```powershell
# Convert .pfx to base64 (GitHub secret format)
$certBytes = [System.IO.File]::ReadAllBytes("path\to\cert.pfx")
$certBase64 = [System.Convert]::ToBase64String($certBytes)
$certBase64 | Set-Clipboard
```

### Step 2: Add Secrets to GitHub

Go to **Settings → Secrets and variables → Actions → New repository secret**:

| Secret Name | Value | Description |
|-------------|-------|-------------|
| `WINDOWS_CERTIFICATE` | `[paste base64]` | Base64-encoded `.pfx` file |
| `CERTIFICATE_PASSWORD` | `YourPassword` | Password for `.pfx` file |

**Security:** Secrets are encrypted at rest and only exposed during workflow runs.

### Step 3: Trigger Release Workflow

The workflow (`.github/workflows/release-windows.yml`) triggers automatically on:
- **Release published** — Manual release creation on GitHub
- **Workflow dispatch** — Manual trigger with version input

**To create a release:**
```bash
# Tag the release
git tag -a v0.1.0 -m "Release v0.1.0"
git push origin v0.1.0

# Create release on GitHub
gh release create v0.1.0 --title "YOLOTerm v0.1.0" --notes-file CHANGELOG_WINDOWS.md
```

The workflow will:
1. Build x64 and ARM64 binaries
2. Run tests and benchmarks
3. Sign executables with Authenticode
4. Create and sign MSIX packages
5. Create portable ZIPs
6. Upload artifacts to GitHub Release

---

## MSIX Packaging Details

### Package Identity

Edit `windows/Package.appxmanifest` to match your certificate:

```xml
<Identity
  Name="YOLOVibeCode.YOLOTerm"
  Publisher="CN=YourOrganization"  <!-- MUST match certificate CN -->
  Version="0.1.0.0" />
```

**Get certificate CN:**
```powershell
$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2("path\to\cert.pfx", "password")
$cert.Subject  # e.g., "CN=YOLOVibeCode, O=YOLOVibeCode, C=US"
```

### Assets

MSIX requires image assets in `windows/Assets/`:
- `Square150x150Logo.png` — 150×150px
- `Square44x44Logo.png` — 44×44px
- `Wide310x150Logo.png` — 310×150px
- `StoreLogo.png` — 50×50px
- `SplashScreen.png` — 620×300px

**Placeholder generation:**
```powershell
# Create placeholder images (replace with actual logo)
$placeholderScript = @"
Add-Type -AssemblyName System.Drawing
\$sizes = @(44, 50, 150, 300, 620)
foreach (\$size in \$sizes) {
    \$bmp = New-Object System.Drawing.Bitmap \$size, \$size
    \$gfx = [System.Drawing.Graphics]::FromImage(\$bmp)
    \$gfx.Clear([System.Drawing.Color]::DodgerBlue)
    \$bmp.Save("windows/Assets/Placeholder_\${size}x\${size}.png")
}
"@
```

---

## Winget Submission

After the first release, submit to the **Windows Package Manager** community repository.

### Step 1: Fork winget-pkgs

```bash
# Fork https://github.com/microsoft/winget-pkgs
git clone https://github.com/YOUR_USERNAME/winget-pkgs
cd winget-pkgs
```

### Step 2: Create Manifest

```bash
# Create directory
mkdir -p manifests/y/YOLOVibeCode/YOLOTerm/0.1.0
cd manifests/y/YOLOVibeCode/YOLOTerm/0.1.0

# Copy template
cp ../../../../../windows/winget-manifest.yaml YOLOVibeCode.YOLOTerm.yaml
```

### Step 3: Update SHA256 Hashes

```powershell
# Download MSIX from GitHub Release
$url = "https://github.com/yolovibecode/yoloterm/releases/download/v0.1.0/YOLOTerm-0.1.0-x64.msix"
$file = "YOLOTerm-0.1.0-x64.msix"
Invoke-WebRequest -Uri $url -OutFile $file

# Calculate SHA256
$hash = (Get-FileHash $file -Algorithm SHA256).Hash
Write-Output "InstallerSha256: $hash"
```

Update manifest:
```yaml
InstallerSha256: [PASTE_HASH_HERE]
SignatureSha256: [LEAVE_BLANK_FOR_NOW]
```

### Step 4: Submit PR

```bash
git add .
git commit -m "New package: YOLOVibeCode.YOLOTerm version 0.1.0"
git push origin main

# Create PR to microsoft/winget-pkgs
```

**Validation:** Automated checks will validate manifest format and hash. Merges typically happen within 1–3 business days.

**Reference:** https://github.com/microsoft/winget-pkgs/blob/master/AUTHORING_MANIFESTS.md

---

## Troubleshooting

### Issue: "The app package must be signed..." (MSIX install)

**Cause:** Certificate not trusted on local machine.

**Fix 1:** Enable Developer Mode (Settings → Update & Security → For developers)

**Fix 2:** Trust certificate manually:
```powershell
# Export certificate from .pfx
$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2("cert.pfx", "password")
Export-Certificate -Cert $cert -FilePath cert.cer

# Import to Trusted Root
Import-Certificate -FilePath cert.cer -CertStoreLocation Cert:\LocalMachine\Root
```

### Issue: signtool.exe not found

**Cause:** Windows SDK not installed or not in PATH.

**Fix:**
```powershell
# Find all signtool.exe installations
Get-ChildItem "C:\Program Files (x86)\Windows Kits" -Recurse -Filter signtool.exe

# Add to PATH (session-specific)
$env:PATH += ";C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64"
```

### Issue: Timestamp server timeout

**Cause:** Network issue or DigiCert server overload.

**Fix:** Try alternative timestamp servers:
```powershell
# DigiCert (primary)
/tr http://timestamp.digicert.com

# Sectigo
/tr http://timestamp.sectigo.com

# GlobalSign
/tr http://timestamp.globalsign.com/tsa/r6advanced1
```

### Issue: "Publisher name does not match certificate"

**Cause:** `Package.appxmanifest` Publisher CN doesn't match certificate CN.

**Fix:**
```powershell
# Extract CN from certificate
$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2("cert.pfx", "password")
$cert.Subject  # Update manifest to match this
```

---

## Security Notes

### Certificate Storage

**❌ NEVER commit certificates to Git:**
```bash
# .gitignore already includes:
*.pfx
*.p12
*.cer
cert.*
```

**✅ Recommended storage:**
- **Local:** Windows Certificate Store (certmgr.msc → Personal → Certificates)
- **CI:** GitHub Encrypted Secrets (base64-encoded)
- **Production:** Azure Key Vault or AWS Secrets Manager

### Password Management

**❌ NEVER hardcode passwords:**
```powershell
# BAD
& $signtool sign /f cert.pfx /p "MyPassword123" ...
```

**✅ Use environment variables or secret management:**
```powershell
# GOOD (local)
$certPass = Read-Host "Certificate password" -AsSecureString
$certPass = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($certPass))

# GOOD (CI)
/p "${{ secrets.CERTIFICATE_PASSWORD }}"
```

### Certificate Expiry

- **Typical validity:** 1–3 years
- **Monitoring:** Set calendar reminder 1 month before expiry
- **Renewal:** Coordinate with CA; may require re-validation
- **Timestamping:** Even after certificate expires, timestamped signatures remain valid

**Check expiry:**
```powershell
$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2("cert.pfx", "password")
$cert.NotAfter  # e.g., "6/12/2027"
```

---

## Summary Checklist

Before first release:
- [ ] Acquire code signing certificate (EV/OV)
- [ ] Test local signing (`signtool.exe` + `makeappx.exe`)
- [ ] Verify MSIX installs on clean Windows VM
- [ ] Configure GitHub secrets (`WINDOWS_CERTIFICATE`, `CERTIFICATE_PASSWORD`)
- [ ] Create GitHub release (triggers workflow)
- [ ] Download and test signed artifacts
- [ ] Submit to winget (after first release)
- [ ] Document certificate renewal date

---

**Questions?**
File an issue: https://github.com/yolovibecode/yoloterm/issues

**License:** MIT

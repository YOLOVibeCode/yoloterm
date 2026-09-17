# Release Guide

This document explains how to configure and execute a YOLOTerm release for macOS.

## Overview

The YOLOTerm release pipeline is automated via GitHub Actions (`.github/workflows/release.yml`). The workflow handles:

1. **Building** the app bundle (universal binary for Apple Silicon and Intel)
2. **Testing** all test suites
3. **Code signing** with Developer ID Application certificate
4. **Notarization** via Apple's notary service
5. **Stapling** the notarization ticket to the app
6. **DMG creation** with drag-to-Applications layout
7. **Sparkle appcast** generation with EdDSA signature
8. **GitHub Release** creation with downloadable DMG

Steps 3-7 require credentials that you must provide. The workflow will skip these steps gracefully if credentials are not configured, but you'll need them for a production release.

---

## Prerequisites

### 1. Apple Developer ID Application Certificate

**What it's for:** Code signing the app bundle so macOS Gatekeeper trusts it.

**How to obtain:**

1. Join the [Apple Developer Program](https://developer.apple.com/programs/) ($99/year)
2. Go to [Certificates, Identifiers & Profiles](https://developer.apple.com/account/resources/certificates/list)
3. Create a new certificate:
   - Type: **Developer ID Application**
   - Follow the instructions to generate a Certificate Signing Request (CSR) from Keychain Access
   - Upload the CSR and download the certificate
4. Export the certificate from Keychain Access:
   - Open Keychain Access
   - Find your "Developer ID Application" certificate
   - Right-click → Export
   - Save as `.p12` file with a password

**Add to GitHub Secrets:**

```bash
# Base64-encode the certificate
cat /path/to/certificate.p12 | base64 | pbcopy

# Add to GitHub repository secrets:
# - MACOS_CERTIFICATE: (paste the base64 output)
# - MACOS_CERTIFICATE_PWD: (the password you set when exporting)
# - KEYCHAIN_PASSWORD: (any strong password for temporary keychain)
```

### 2. App Store Connect API Key (for Notarization)

**What it's for:** Submitting the app to Apple's notary service for malware scanning.

**How to obtain:**

1. Go to [App Store Connect](https://appstoreconnect.apple.com/)
2. Navigate to **Users and Access** → **Keys** tab (under "In-App Purchase")
3. Click **+** to generate a new API key
4. Select **Access: Developer** role
5. Download the `.p8` key file (you can only download it once!)
6. Note the **Issuer ID** and **Key ID**

**Add to GitHub Secrets:**

```bash
# Add to GitHub repository secrets:
# - NOTARIZATION_APPLE_ID: your Apple ID email (e.g., you@example.com)
# - NOTARIZATION_TEAM_ID: your Team ID (10-character string, find it in App Store Connect)
# - NOTARIZATION_PASSWORD: app-specific password (see below)
```

**Generate an app-specific password:**

1. Go to [appleid.apple.com](https://appleid.apple.com/)
2. Sign in
3. Navigate to **Security** → **App-Specific Passwords**
4. Generate a new password (label it "YOLOTerm Notarization")
5. Copy the password and save it as `NOTARIZATION_PASSWORD` secret

### 3. Sparkle EdDSA Keypair (for Auto-Updates)

**What it's for:** Signing the appcast feed so YOLOTerm can verify updates.

**How to generate:**

```bash
# Install Sparkle tools
brew install sparkle

# Generate EdDSA keypair
/opt/homebrew/bin/generate_keys

# This outputs:
# - Public key (add to YOLOTerm's Info.plist as SUPublicEDKey)
# - Private key (keep secret, add to GitHub Secrets as SPARKLE_PRIVATE_KEY)
```

**Add to GitHub Secrets:**

```bash
# Copy the private key (the long base64 string)
# Add to GitHub repository secrets:
# - SPARKLE_PRIVATE_KEY: (paste the private key)
```

**Update Info.plist:**

Add the public key to `macos/YOLOTermApp/Info.plist`:

```xml
<key>SUPublicEDKey</key>
<string>YOUR_PUBLIC_KEY_HERE</string>
<key>SUFeedURL</key>
<string>https://github.com/yourusername/yoloterm/releases/latest/download/appcast.xml</string>
```

### 4. Configure GitHub Secrets

Go to your GitHub repository → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**.

Add the following secrets:

| Secret Name                | Description                              |
|----------------------------|------------------------------------------|
| `MACOS_CERTIFICATE`        | Base64-encoded .p12 certificate          |
| `MACOS_CERTIFICATE_PWD`    | Password for the .p12 file               |
| `KEYCHAIN_PASSWORD`        | Temporary keychain password (any string) |
| `NOTARIZATION_APPLE_ID`    | Your Apple ID email                      |
| `NOTARIZATION_TEAM_ID`     | Your Apple Developer Team ID             |
| `NOTARIZATION_PASSWORD`    | App-specific password                    |
| `SPARKLE_PRIVATE_KEY`      | Sparkle EdDSA private key                |

---

## Creating a Release

### Automated Release (Recommended)

1. **Tag the release:**

   ```bash
   git tag -a v0.1.0 -m "Release v0.1.0"
   git push origin v0.1.0
   ```

2. The GitHub Actions workflow will automatically:
   - Build the app
   - Run tests
   - Sign and notarize (if credentials are configured)
   - Create a DMG
   - Create a draft GitHub Release

3. **Review and publish:**
   - Go to [GitHub Releases](https://github.com/yourusername/yoloterm/releases)
   - Find the draft release
   - Edit the release notes if needed
   - Click **Publish release**

### Manual Release

If credentials aren't configured or you prefer manual steps:

1. **Build the app:**

   ```bash
   cd macos
   swift build -c release --arch arm64 --arch x86_64
   ```

2. **Create app bundle:**

   ```bash
   mkdir -p build/Release/YOLOTerm.app/Contents/MacOS
   mkdir -p build/Release/YOLOTerm.app/Contents/Resources
   cp .build/release/YOLOTerm build/Release/YOLOTerm.app/Contents/MacOS/
   cp YOLOTermApp/Info.plist build/Release/YOLOTerm.app/Contents/
   ```

3. **Code sign:**

   ```bash
   codesign --force --sign "Developer ID Application: Your Name (TEAMID)" \
     --options runtime --deep build/Release/YOLOTerm.app
   ```

4. **Create archive:**

   ```bash
   ditto -c -k --keepParent build/Release/YOLOTerm.app build/YOLOTerm.zip
   ```

5. **Submit for notarization:**

   ```bash
   xcrun notarytool submit build/YOLOTerm.zip \
     --apple-id "your@email.com" \
     --team-id "TEAMID" \
     --password "app-specific-password" \
     --wait
   ```

6. **Staple notarization:**

   ```bash
   xcrun stapler staple build/Release/YOLOTerm.app
   xcrun stapler validate build/Release/YOLOTerm.app
   ```

7. **Create DMG:**

   ```bash
   brew install create-dmg
   
   create-dmg \
     --volname "YOLOTerm" \
     --window-pos 200 120 \
     --window-size 800 400 \
     --icon-size 100 \
     --icon "YOLOTerm.app" 200 190 \
     --hide-extension "YOLOTerm.app" \
     --app-drop-link 600 185 \
     "build/YOLOTerm-0.1.0.dmg" \
     "build/Release/YOLOTerm.app"
   ```

8. **Generate Sparkle signature:**

   ```bash
   /opt/homebrew/bin/sign_update build/YOLOTerm-0.1.0.dmg
   ```

9. **Create GitHub Release manually** and upload the DMG

---

## Release Checklist

Before releasing, ensure:

- [ ] All CI jobs are green
- [ ] Version number updated in `Info.plist`
- [ ] `CHANGELOG.md` updated with release notes
- [ ] `README.md` reflects current features
- [ ] Performance benchmarks meet targets (see `BENCHMARKS.md`)
- [ ] Clean-machine test passed (install DMG on a Mac without dev tools)
- [ ] Claude Code renders in full color (the founding bug — test every release)
- [ ] All menu actions work correctly
- [ ] Workspace and history persistence work

---

## Troubleshooting

### Code signing fails

**Error:** `codesign: no identity found`

**Solution:** Ensure your Developer ID Application certificate is installed in your Keychain. You may need to download it from Apple Developer portal.

### Notarization fails

**Error:** `The binary is not signed with a valid Developer ID certificate`

**Solution:** You must sign with a Developer ID Application certificate, not a development certificate.

**Error:** `Invalid credentials`

**Solution:** Verify your Apple ID, Team ID, and app-specific password are correct. App-specific passwords expire; generate a new one if needed.

### DMG creation fails

**Error:** `Device busy`

**Solution:** This is a known issue with `create-dmg`. The workflow handles it gracefully. If the DMG exists, it succeeded.

### Stapling fails

**Error:** `The staple and validate action failed!`

**Solution:** Notarization must complete successfully before stapling. Wait for notarization to finish (use `--wait` flag).

---

## Sparkle Auto-Updates

Once Sparkle is configured, YOLOTerm will check for updates on launch.

**Appcast XML structure:**

Create `appcast.xml` in your repository (or host elsewhere):

```xml
<?xml version="1.0" encoding="utf-8"?>
<rss version="2.0" xmlns:sparkle="http://www.andymatuschak.org/xml-namespaces/sparkle">
  <channel>
    <title>YOLOTerm Updates</title>
    <link>https://github.com/yourusername/yoloterm</link>
    <description>YOLOTerm release feed</description>
    <language>en</language>
    <item>
      <title>Version 0.1.0</title>
      <sparkle:version>0.1.0</sparkle:version>
      <sparkle:releaseNotesLink>https://github.com/yourusername/yoloterm/releases/tag/v0.1.0</sparkle:releaseNotesLink>
      <pubDate>Fri, 12 Jun 2026 12:00:00 -0500</pubDate>
      <enclosure
        url="https://github.com/yourusername/yoloterm/releases/download/v0.1.0/YOLOTerm-0.1.0.dmg"
        sparkle:version="0.1.0"
        sparkle:edSignature="YOUR_SIGNATURE_HERE"
        length="FILE_SIZE_IN_BYTES"
        type="application/octet-stream" />
    </item>
  </channel>
</rss>
```

The workflow generates `appcast-entry.xml` for each release; manually merge it into your main `appcast.xml`.

---

## Security Notes

- **Never commit secrets to the repository**
- GitHub Secrets are encrypted and only exposed during workflow runs
- The temporary keychain created during signing is destroyed after the job
- Sparkle's EdDSA signature ensures update integrity

---

## Support

For issues or questions:
- GitHub Issues: https://github.com/yourusername/yoloterm/issues
- Discussions: https://github.com/yourusername/yoloterm/discussions

---

## References

- [Apple Code Signing Guide](https://developer.apple.com/support/code-signing/)
- [Apple Notarization Guide](https://developer.apple.com/documentation/security/notarizing_macos_software_before_distribution)
- [Sparkle Documentation](https://sparkle-project.org/documentation/)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)

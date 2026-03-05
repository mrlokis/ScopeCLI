<#
.SYNOPSIS
    Graphical installer for ScopeCLI on Windows.

.DESCRIPTION
    This script provides a user-friendly wizard to download and install
    ScopeLauncher.exe from the official GitHub repository. It allows you to
    choose an installation folder, monitors the download progress, and
    automatically creates a desktop shortcut upon successful completion.

    The installer is written in pure PowerShell using Windows Forms and
    runs on any Windows machine with PowerShell 3.0 or later.

.PARAMETER None
    This script does not accept command-line parameters. All settings are
    configured via the graphical interface.

.EXAMPLE
    .\ScopeCLI-Installer.ps1
    Launches the installer window. Follow the on-screen instructions.

.NOTES
    Author      : Based on community script
    Version     : 1.0
    Requirements: Windows PowerShell 3.0+, Internet connection,
                  Execution policy allowing scripts (or use bypass).

    The downloaded executable is placed in the selected folder and a
    shortcut named "ScopeLauncher.lnk" is created on the desktop.

.LINK
    https://github.com/mrlokis/mc-launcher-test/releases/download/0.00.1/ScopeLauncher.exe
#>

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.IO

Set-Alias -Name new -Value New-Object

$form = new System.Windows.Forms.Form
$form.Text = "ScopeCLI Installer script for Windows PowerShell"
$form.Size = new System.Drawing.Size(720, 480)
$form.StartPosition = "CenterScreen"
$form.FormBorderStyle = "FixedDialog"
$form.MaximizeBox = $false

$label = new System.Windows.Forms.Label
$label.Text = "Select folder to install ScopeCLI:"
$label.Location = new System.Drawing.Point(10, 20)
$label.Size = new System.Drawing.Size(280, 20)
$form.Controls.Add($label)

$textBoxPath = new System.Windows.Forms.TextBox
$textBoxPath.Location = new System.Drawing.Point(10, 45)
$textBoxPath.Size = new System.Drawing.Size(500, 20)
$textBoxPath.Text = [System.IO.Path]::Combine($env:ProgramFiles, "ScopeCLI")
$form.Controls.Add($textBoxPath)

$buttonBrowse = new System.Windows.Forms.Button
$buttonBrowse.Text = "Browse..."
$buttonBrowse.Location = new System.Drawing.Point(520, 43)
$buttonBrowse.Size = new System.Drawing.Size(75, 23)
$buttonBrowse.Add_Click({
    $folderBrowser = new System.Windows.Forms.FolderBrowserDialog
    $folderBrowser.Description = "Select folder to install ScopeCLI"
    $folderBrowser.SelectedPath = $textBoxPath.Text
    if ($folderBrowser.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        $textBoxPath.Text = $folderBrowser.SelectedPath
    }
})
$form.Controls.Add($buttonBrowse)

$progressBar = new System.Windows.Forms.ProgressBar
$progressBar.Location = new System.Drawing.Point(10, 80)
$progressBar.Size = new System.Drawing.Size(680, 23)
$progressBar.Style = "Marquee"
$progressBar.Visible = $false
$form.Controls.Add($progressBar)

$statusLabel = new System.Windows.Forms.Label
$statusLabel.Location = new System.Drawing.Point(10, 110)
$statusLabel.Size = new System.Drawing.Size(680, 30)
$statusLabel.Text = ""
$form.Controls.Add($statusLabel)

$buttonInstall = new System.Windows.Forms.Button
$buttonInstall.Text = "Install"
$buttonInstall.Location = new System.Drawing.Point(420, 400)
$buttonInstall.Size = new System.Drawing.Size(75, 23)
$buttonInstall.Add_Click({
    $buttonInstall.Enabled = $false
    $buttonBrowse.Enabled = $false
    $textBoxPath.Enabled = $false

    $progressBar.Style = "Continuous"
    $progressBar.Minimum = 0
    $progressBar.Maximum = 100
    $progressBar.Value = 0
    $progressBar.Visible = $true

    $statusLabel.Text = "Preparing to download..."
    $form.Refresh()

    $installPath = $textBoxPath.Text
    $exeName = "ScopeCLI.exe"
    $exeFullPath = [System.IO.Path]::Combine($installPath, $exeName)
    $url = "https://github.com/mrlokis/ScopeCLI/releases/download/0.00.1/ScopeCLI.exe"

    $script:targetExePath = $exeFullPath
    $script:targetInstallPath = $installPath

    try {
        if (-not (Test-Path $installPath)) {
            New-Item -ItemType Directory -Path $installPath -Force | Out-Null
        }

        $webClient = New-Object System.Net.WebClient

        $webClient.add_DownloadProgressChanged({
            param($sender, $e)
            $form.Invoke([Action]{
                $progressBar.Value = $e.ProgressPercentage
                $statusLabel.Text = "Downloaded: $($e.ProgressPercentage)%"
                $form.Refresh()
            })
        })

        $webClient.add_DownloadFileCompleted({
            param($sender, $e)
            $form.Invoke([Action]{
                if ($e.Error) {
                    $statusLabel.Text = "Error: Download failed!"
                    [System.Windows.Forms.MessageBox]::Show("Error during download: $($e.Error.Message)", "Download Error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)

                    $buttonInstall.Enabled = $true
                    $buttonBrowse.Enabled = $true
                    $textBoxPath.Enabled = $true
                    $progressBar.Visible = $false
                }
                elseif ($e.Cancelled) {
                    $statusLabel.Text = "Download cancelled."
                    $buttonInstall.Enabled = $true
                    $buttonBrowse.Enabled = $true
                    $textBoxPath.Enabled = $true
                    $progressBar.Visible = $false
                }
                else {
                    $statusLabel.Text = "Download complete. Creating desktop shortcut..."
                    $form.Refresh()

                    $exeFullPath = $script:targetExePath
                    $installPath = $script:targetInstallPath

                    if ([string]::IsNullOrWhiteSpace($exeFullPath)) {
                        [System.Windows.Forms.MessageBox]::Show("Error: Executable path is empty. (Debug: $exeFullPath)", "Installation Error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
                        $buttonInstall.Enabled = $true
                        $buttonBrowse.Enabled = $true
                        $textBoxPath.Enabled = $true
                        $progressBar.Visible = $false
                        return
                    }

                    if ($exeFullPath.Length -gt 260) {
                        [System.Windows.Forms.MessageBox]::Show("Error: The installation path is too long ($($exeFullPath.Length) characters). Maximum allowed is 260.`nPlease choose a shorter folder (e.g., directly on C:\ or D:\).", "Installation Error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
                        $buttonInstall.Enabled = $true
                        $buttonBrowse.Enabled = $true
                        $textBoxPath.Enabled = $true
                        $progressBar.Visible = $false
                        return
                    }

                    $invalidChars = [System.IO.Path]::GetInvalidPathChars()
                    if ($exeFullPath.IndexOfAny($invalidChars) -ne -1) {
                        [System.Windows.Forms.MessageBox]::Show("Error: The installation path contains invalid characters.", "Installation Error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
                        $buttonInstall.Enabled = $true
                        $buttonBrowse.Enabled = $true
                        $textBoxPath.Enabled = $true
                        $progressBar.Visible = $false
                        return
                    }

                    if (-not (Test-Path $exeFullPath)) {
                        [System.Windows.Forms.MessageBox]::Show("Error: The downloaded file was not found at:`n$exeFullPath", "Installation Error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
                        $buttonInstall.Enabled = $true
                        $buttonBrowse.Enabled = $true
                        $textBoxPath.Enabled = $true
                        $progressBar.Visible = $false
                        return
                    }

                    try {
                        $desktopPath = [System.IO.Path]::Combine([Environment]::GetFolderPath("Desktop"), "ScopeLauncher.lnk")
                        $wScriptShell = New-Object -ComObject WScript.Shell
                        $shortcut = $wScriptShell.CreateShortcut($desktopPath)
                        $shortcut.TargetPath = $exeFullPath
                        $shortcut.WorkingDirectory = $installPath
                        $shortcut.Description = "ScopeCLI Launcher"
                        $shortcut.Save()

                        $statusLabel.Text = "Installation completed successfully!"
                        [System.Windows.Forms.MessageBox]::Show("ScopeCLI has been installed successfully to:`n$installPath`n`nA shortcut has been created on your desktop.", "Installation Complete", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)

                        $form.DialogResult = [System.Windows.Forms.DialogResult]::OK
                        $form.Close()
                    }
                    catch {
                        $statusLabel.Text = "Error: Failed to create shortcut!"
                        [System.Windows.Forms.MessageBox]::Show("Error creating desktop shortcut: $($_.Exception.Message)", "Shortcut Error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
                        $buttonInstall.Enabled = $true
                        $buttonBrowse.Enabled = $true
                        $textBoxPath.Enabled = $true
                        $progressBar.Visible = $false
                    }
                }
            })
        })

        $webClient.DownloadFileAsync($url, $exeFullPath)
    }
    catch {
        $statusLabel.Text = "Error: Installation failed!"
        [System.Windows.Forms.MessageBox]::Show("Error during installation: $($_.Exception.Message)", "Installation Error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)

        $buttonInstall.Enabled = $true
        $buttonBrowse.Enabled = $true
        $textBoxPath.Enabled = $true
        $progressBar.Visible = $false
    }
})
$form.Controls.Add($buttonInstall)

$buttonCancel = new System.Windows.Forms.Button
$buttonCancel.Text = "Cancel"
$buttonCancel.Location = new System.Drawing.Point(510, 400)
$buttonCancel.Size = new System.Drawing.Size(75, 23)
$buttonCancel.Add_Click({
    $form.DialogResult = [System.Windows.Forms.DialogResult]::Cancel
    $form.Close()
})
$form.Controls.Add($buttonCancel)

$result = $form.ShowDialog()

if ($result -eq [System.Windows.Forms.DialogResult]::OK) {
    Write-Host "Installation completed to: $($textBoxPath.Text)"
}
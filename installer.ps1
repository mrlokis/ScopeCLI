Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.IO

Set-Alias -Name new -Value New-Object

$form = new System.Windows.Forms.Form
$form.Text = "ScopeCLI Installer script for Windows PowerShell"
$form.Size = new System.Drawing.Size(720, 480)
$form.StartPosition = "CenterScreen"
$form.FormBorderStyle = "FixedSingle"
$form.MaximizeBox = $false

# Label
$label = new System.Windows.Forms.Label
$label.Text = "Select folder to install ScopeCLI:"
$label.Location = new System.Drawing.Point(10, 20)
$label.Size = new System.Drawing.Size(280, 20)
$form.Controls.Add($label)

# Path textbox (can be edited)
$textBoxPath = new System.Windows.Forms.TextBox
$textBoxPath.Location = new System.Drawing.Point(10, 45)
$textBoxPath.Size = new System.Drawing.Size(500, 20)
$textBoxPath.Text = [System.IO.Path]::Combine($env:ProgramFiles, "ScopeCLI")  # default path
$form.Controls.Add($textBoxPath)

# Browse button
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

# Progress bar (initially Marquee style, will be changed during install)
$progressBar = new System.Windows.Forms.ProgressBar
$progressBar.Location = new System.Drawing.Point(10, 80)
$progressBar.Size = new System.Drawing.Size(680, 23)
$progressBar.Style = "Marquee"
$progressBar.Visible = $false
$form.Controls.Add($progressBar)

# Status label
$statusLabel = new System.Windows.Forms.Label
$statusLabel.Location = new System.Drawing.Point(10, 110)
$statusLabel.Size = new System.Drawing.Size(680, 30)
$statusLabel.Text = ""
$form.Controls.Add($statusLabel)

# Install button
$buttonInstall = new System.Windows.Forms.Button
$buttonInstall.Text = "Install"
$buttonInstall.Location = new System.Drawing.Point(420, 400)
$buttonInstall.Size = new System.Drawing.Size(75, 23)
$buttonInstall.Add_Click({
    # Disable buttons during installation
    $buttonInstall.Enabled = $false
    $buttonBrowse.Enabled = $false
    $textBoxPath.Enabled = $false

    # Configure progress bar for percentage display
    $progressBar.Style = "Continuous"
    $progressBar.Minimum = 0
    $progressBar.Maximum = 100
    $progressBar.Value = 0
    $progressBar.Visible = $true

    $statusLabel.Text = "Preparing to download..."
    $form.Refresh()

    $installPath = $textBoxPath.Text
    $exeName = "ScopeLauncher.exe"
    $exeFullPath = [System.IO.Path]::Combine($installPath, $exeName)
    $url = "https://github.com/mrlokis/mc-launcher-test/releases/download/0.00.1/ScopeLauncher.exe"

    # Сохраняем путь в области скрипта для доступа из событий
    $script:targetExePath = $exeFullPath
    $script:targetInstallPath = $installPath

    try {
        # Create directory if it doesn't exist
        if (-not (Test-Path $installPath)) {
            New-Item -ItemType Directory -Path $installPath -Force | Out-Null
        }

        # Use WebClient for asynchronous download with progress
        $webClient = New-Object System.Net.WebClient

        # Progress event (PowerShell 5.1 compatible)
        $webClient.add_DownloadProgressChanged({
            param($sender, $e)
            # Update UI on the main thread
            $form.Invoke([Action]{
                $progressBar.Value = $e.ProgressPercentage
                $statusLabel.Text = "Downloaded: $($e.ProgressPercentage)%"
                $form.Refresh()
            })
        })

        # Completion event
        $webClient.add_DownloadFileCompleted({
            param($sender, $e)
            $form.Invoke([Action]{
                if ($e.Error) {
                    # Download error
                    $statusLabel.Text = "Error: Download failed!"
                    [System.Windows.Forms.MessageBox]::Show("Error during download: $($e.Error.Message)", "Download Error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)

                    # Re-enable controls
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
                    # Download completed successfully
                    $statusLabel.Text = "Download complete. Creating desktop shortcut..."
                    $form.Refresh()

                    # Используем переменные из области скрипта
                    $exeFullPath = $script:targetExePath
                    $installPath = $script:targetInstallPath

                    # --- ПРОВЕРКИ ПУТИ ПЕРЕД СОЗДАНИЕМ ЯРЛЫКА ---
                    # 1. Проверка на пустой путь (с отладкой)
                    if ([string]::IsNullOrWhiteSpace($exeFullPath)) {
                        [System.Windows.Forms.MessageBox]::Show("Error: Executable path is empty. (Debug: $exeFullPath)", "Installation Error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
                        $buttonInstall.Enabled = $true
                        $buttonBrowse.Enabled = $true
                        $textBoxPath.Enabled = $true
                        $progressBar.Visible = $false
                        return
                    }

                    # 2. Проверка длины пути (макс. 260 символов для классических приложений)
                    if ($exeFullPath.Length -gt 260) {
                        [System.Windows.Forms.MessageBox]::Show("Error: The installation path is too long ($($exeFullPath.Length) characters). Maximum allowed is 260.`nPlease choose a shorter folder (e.g., directly on C:\ or D:\).", "Installation Error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
                        $buttonInstall.Enabled = $true
                        $buttonBrowse.Enabled = $true
                        $textBoxPath.Enabled = $true
                        $progressBar.Visible = $false
                        return
                    }

                    # 3. Проверка на недопустимые символы в пути
                    $invalidChars = [System.IO.Path]::GetInvalidPathChars()
                    if ($exeFullPath.IndexOfAny($invalidChars) -ne -1) {
                        [System.Windows.Forms.MessageBox]::Show("Error: The installation path contains invalid characters.", "Installation Error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
                        $buttonInstall.Enabled = $true
                        $buttonBrowse.Enabled = $true
                        $textBoxPath.Enabled = $true
                        $progressBar.Visible = $false
                        return
                    }

                    # 4. Проверка существования загруженного файла
                    if (-not (Test-Path $exeFullPath)) {
                        [System.Windows.Forms.MessageBox]::Show("Error: The downloaded file was not found at:`n$exeFullPath", "Installation Error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
                        $buttonInstall.Enabled = $true
                        $buttonBrowse.Enabled = $true
                        $textBoxPath.Enabled = $true
                        $progressBar.Visible = $false
                        return
                    }

                    # --- СОЗДАНИЕ ЯРЛЫКА ---
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

        # Start asynchronous download (does not block UI)
        $webClient.DownloadFileAsync($url, $exeFullPath)
    }
    catch {
        # Handle synchronous errors (e.g., folder creation)
        $statusLabel.Text = "Error: Installation failed!"
        [System.Windows.Forms.MessageBox]::Show("Error during installation: $($_.Exception.Message)", "Installation Error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)

        # Re-enable controls
        $buttonInstall.Enabled = $true
        $buttonBrowse.Enabled = $true
        $textBoxPath.Enabled = $true
        $progressBar.Visible = $false
    }
})
$form.Controls.Add($buttonInstall)

# Cancel button
$buttonCancel = new System.Windows.Forms.Button
$buttonCancel.Text = "Cancel"
$buttonCancel.Location = new System.Drawing.Point(510, 400)
$buttonCancel.Size = new System.Drawing.Size(75, 23)
$buttonCancel.Add_Click({
    $form.DialogResult = [System.Windows.Forms.DialogResult]::Cancel
    $form.Close()
})
$form.Controls.Add($buttonCancel)

# Show the form
$result = $form.ShowDialog()

if ($result -eq [System.Windows.Forms.DialogResult]::OK) {
    Write-Host "Installation completed to: $($textBoxPath.Text)"
}
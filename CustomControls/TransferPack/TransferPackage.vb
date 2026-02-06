Imports System.Configuration
Imports System.IO
Imports System.IO.Compression
Imports System.Text.Json
Imports System.Windows
Imports ElnBase.ELNEnumerations
Imports Microsoft.Win32

Public Class TransferPackage

    ''' <summary>
    ''' Creates a package for the transfer of current experiments and settings to another ELN installation.
    ''' </summary>
    '''
    Public Shared Sub Create(currUserID As String)

        Dim transferDlg As New dlgCreateTransferPack With {.Owner = Application.Current.MainWindow}

        If transferDlg.ShowDialog Then

            Dim saveDlg As New SaveFileDialog
            With saveDlg

                .Title = "Create Experiment Transfer Package"
                .Filter = "Phoenix ELN Transfer Package (*.elnpkg)|*.elnpkg"
                .InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                .FileName = currUserID + "_Migration.elnpkg"

                If .ShowDialog() = True Then
                    PackElnData(.FileName)
                End If

            End With

        End If

    End Sub


    ''' <summary>
    ''' Packs the current ELN experiments and settings into a package specified by the specified dstFilePath.
    ''' </summary>
    ''' 
    Private Shared Function PackElnData(dstFilePath As String, Optional isRecoveryFile As Boolean = False) As Boolean

        Dim dbFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\Phoenix ELN Data"
        Dim tempDir = Path.Combine(Path.GetTempPath(), "PhoenixElnTempFolder")

        If Directory.Exists(tempDir) Then
            Directory.Delete(tempDir, recursive:=True)
        End If

        ' copying is required for including any locked files (such as the database file) into the zip 
        CopyDirectory(dbFolderPath, tempDir)

        ' convert settings to jsonStr file and also add it to the temp folder
        Dim settingsFilePath = Path.Combine(tempDir, "_MySettings.json")
        ExportMySettings(settingsFilePath)

        ' add a recovery file marker if applicable
        If isRecoveryFile Then
            Dim recoveryMarkerPath = Path.Combine(tempDir, "_isRecoveryFile.txt")
            File.WriteAllText(recoveryMarkerPath, DateTime.Now.ToString())
        End If

        If File.Exists(dstFilePath) Then
            File.Delete(dstFilePath)
        End If

        ' create a zip file of the Phoenix ELN data folder and save it to the selected location.
        Try

            ZipFile.CreateFromDirectory(tempDir, dstFilePath)

            If Not isRecoveryFile Then
                cbMsgBox.Display("Transfer package successfully created!" + vbCrLf + vbCrLf +
                    "You now can import this package from within your new ELN " + vbCrLf +
                    "installation via Tools > Transfer Package > Import.", MsgBoxStyle.Information, "Transfer Package")
            End If

            Return True

        Catch ex As Exception

            cbMsgBox.Display("Error creating transfer package: " + ex.Message, MsgBoxStyle.Exclamation, "Transfer Package Error")
            Return False

        Finally

            'remove temp folder
            Directory.Delete(tempDir, recursive:=True)

        End Try

    End Function



    ''' <summary>
    ''' Lets the user select a transfer package and imports its contents into his temp folder, 
    ''' ready for migration on subsequent automated application restart.
    ''' </summary>
    ''' 
    Public Shared Function InitializeImport(isDemo As Boolean) As Boolean

        ' warn about overwriting existing data
        Dim importDlg As New dlgImportTransferPack
        With importDlg
            .Owner = Application.Current.MainWindow
            If Not .ShowDialog() Then
                Return False
            End If
        End With

        ' final warning for non-demo mode
        If Not isDemo Then

            Dim resp = cbMsgBox.Display("Your current experiments database contains non-demo " + vbCrLf +
                        "experiments, which may contain productive data. " + vbCrLf +
                        "Importing a transfer package will overwrite them!" + vbCrLf + vbCrLf +
                        "Are you really sure to proceed?",
                        MsgBoxStyle.Exclamation + MsgBoxStyle.OkCancel + MsgBoxStyle.DefaultButton2, "Transfer Package")

            If resp <> MsgBoxResult.Ok Then
                Return False
            End If

        End If

        Dim dbFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\Phoenix ELN Data"

        Dim openDlg As New OpenFileDialog
        With openDlg

            .Title = "Import Experiment Transfer Package"
            .Filter = "Phoenix ELN Transfer Package (*.elnpkg)|*.elnpkg"
            .InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            .FileName = ""

            If .ShowDialog() = True Then

                Dim recoveryFileName = "recovery.elnpkg"
                Dim recoveryPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\" + "recovery.elnpkg"

                ' create temp folder and extract zip contents there
                Dim tempDir = Path.Combine(Path.GetTempPath(), "PhoenixElnTempExtractFolder")
                If Directory.Exists(tempDir) Then
                    Directory.Delete(tempDir, recursive:=True)
                End If

                Try

                    ZipFile.ExtractToDirectory(.FileName, tempDir, overwriteFiles:=True)

                    ' Validation: The transfer file is considered valid if at least the experiments database and the settings file are present.

                    Dim settingsFilePath = Path.Combine(tempDir, "_MySettings.json")
                    Dim dbFilePath = Path.Combine(tempDir, "ElnData.db")
                    Dim recoveryMarkerPath = Path.Combine(tempDir, "_isRecoveryFile.txt")

                    Dim isRecovery As Boolean = False
                    If File.Exists(recoveryMarkerPath) Then
                        isRecovery = True
                        File.Delete(recoveryMarkerPath) 'remove the marker file after detection
                    End If

                    If Not File.Exists(settingsFilePath) OrElse Not File.Exists(dbFilePath) Then
                        cbMsgBox.Display("The selected transfer package is invalid (missing components).", MsgBoxStyle.Exclamation, "Transfer Package Error")
                        Return False
                    End If

                    If Not isDemo AndAlso Not isRecovery Then

                        ' Create recovery file if replacing non-demo experiments with a non-recovery migration (default).

                        If Not PackElnData(recoveryPath, isRecoveryFile:=True) Then

                            cbMsgBox.Display("Could not create recovery transfer package. Import cancelled.", MsgBoxStyle.Exclamation, "Transfer Package Error")
                            Directory.Delete(tempDir, recursive:=True)
                            Return False

                        End If

                        My.Settings.DataMigrationType = MigrationType.ReplaceProductive

                    Else

                        ' No recovery file is created if replacing demo experiments, or if already within recovery process

                        If isRecovery Then
                            My.Settings.DataMigrationType = MigrationType.Recovery
                        Else
                            My.Settings.DataMigrationType = MigrationType.ReplaceDemo
                        End If

                    End If

                    My.Settings.Save()
                    Return True

                Catch ex As Exception

                    cbMsgBox.Display("Error extracting transfer package: " + ex.Message, MsgBoxStyle.Exclamation, "Transfer Package Error")
                    Return False

                End Try

            Else

                Return False

            End If

        End With

    End Function


    ''' <summary>
    ''' Performs the pending migration by copying the extracted transfer package contents
    ''' from the temporary location to the Phoenix ELN data folder, and importing the settings.
    ''' </summary>
    '''
    Public Shared Sub PerformPendingMigration()

        Dim tempFolderPath = Path.Combine(Path.GetTempPath(), "PhoenixElnTempExtractFolder")
        Dim dbFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\Phoenix ELN Data"

        If Directory.Exists(tempFolderPath) Then

            ' Delete existing data folder
            If Directory.Exists(dbFolderPath) Then
                Directory.Delete(dbFolderPath, recursive:=True)
            End If

            ' Copy extracted contents to the Phoenix ELN data folder
            CopyDirectory(tempFolderPath, dbFolderPath)

            ' Remove test folder
            Directory.Delete(tempFolderPath, recursive:=True)

            ' Import settings and delete settings file
            Dim settingsFilePath = Path.Combine(dbFolderPath, "_MySettings.json")
            ImportMySettings(settingsFilePath)

            ' Settings file no longer needed now that settings have been imported
            File.Delete(settingsFilePath)

        End If

    End Sub



    ''' <summary>
    ''' Exports the current settings as serialized JSON to the specified file path with type information preserved
    ''' </summary>
    ''' 
    Private Shared Sub ExportMySettings(exportPath As String)

        Try

            Dim settingsData As New Dictionary(Of String, Object)

            ' Add all user-scoped settings to the dictionary with type information
            For Each prop As SettingsProperty In My.Settings.Properties
                If prop.Attributes(GetType(UserScopedSettingAttribute)) IsNot Nothing Then
                    Dim value As Object = My.Settings(prop.Name)
                    settingsData(prop.Name) = New With {
                        .Type = value.GetType().AssemblyQualifiedName,
                        .Value = value
                    }
                End If
            Next

            ' Serialize the dictionary to JSON
            Dim jsonOptions As New JsonSerializerOptions With {.WriteIndented = True}
            Dim jsonStr As String = JsonSerializer.Serialize(settingsData, jsonOptions)
            File.WriteAllText(exportPath, jsonStr)

        Catch ex As Exception

            Debug.WriteLine($"Error exporting settings: {ex.Message}")

        End Try

    End Sub



    ''' <summary>
    ''' Imports settings from a JSON file with type information preserved
    ''' </summary>
    ''' 
    Private Shared Sub ImportMySettings(importPath As String)

        Try

            Dim jsonStr As String = File.ReadAllText(importPath)
            Dim settingsData As JsonDocument = JsonDocument.Parse(jsonStr)
            Dim root As JsonElement = settingsData.RootElement

            ' Apply settings from the JSON data
            For Each prop As SettingsProperty In My.Settings.Properties

                If prop.Attributes(GetType(UserScopedSettingAttribute)) IsNot Nothing Then

                    Dim key As String = prop.Name
                    Dim jsonValue As JsonElement

                    If root.TryGetProperty(key, jsonValue) Then

                        Try

                            Dim typeStr As String = jsonValue.GetProperty("Type").GetString()
                            Dim valueElement As JsonElement = jsonValue.GetProperty("Value")

                            ' Reconstruct the value with the correct type using AssemblyQualifiedName
                            Dim targetType As Type = Type.GetType(typeStr)

                            If targetType IsNot Nothing Then

                                Dim valueJson As String = valueElement.GetRawText()
                                Dim importedValue As Object = JsonSerializer.Deserialize(valueJson, targetType)

                                'adjust directory paths to valid locations
                                If TypeOf importedValue Is String Then
                                    importedValue = ConvertPath(importedValue)
                                End If

                                My.Settings(key) = importedValue

                                '   Debug.WriteLine($"{key} = {My.Settings(key)} [{targetType}] ")

                            End If

                        Catch ex As Exception

                            Debug.WriteLine($"Warning: Could not import setting '{key}': {ex.Message}")

                        End Try

                    End If

                End If

            Next

            ' Persist the settings to App.config
            My.Settings.Save()

        Catch ex As Exception

            Debug.WriteLine($"Error importing settings: {ex.Message}")

        End Try

    End Sub



    ''' <summary>
    ''' Copies all files and subdirectories from sourceDir to destDir recursively.
    ''' </summary>
    ''' 
    Private Shared Sub CopyDirectory(sourceDir As String, destDir As String)

        Directory.CreateDirectory(destDir)

        ' Copy files
        For Each filePath In Directory.GetFiles(sourceDir)
            Dim fileName = Path.GetFileName(filePath)
            Dim destFile = Path.Combine(destDir, fileName)
            File.Copy(filePath, destFile, overwrite:=True)
        Next

        ' Copy subdirectories recursively
        For Each subDir In Directory.GetDirectories(sourceDir)
            Dim dirName = Path.GetFileName(subDir)
            Dim destSubDir = Path.Combine(destDir, dirName)
            CopyDirectory(subDir, destSubDir)
        Next

    End Sub


    ''' <summary>
    ''' Prevents applying non-existent file paths to the target system settings.
    ''' If the specified string is a valid directory path, it checks if the directory exists.
    ''' If it does not exist, it returns the Desktop path instead.
    ''' If the string is not a path, it returns the original string.     
    ''' </summary>
    ''' 
    Private Shared Function ConvertPath(testStr As String) As String

        Try

            If Path.IsPathRooted(testStr) Then

                If Directory.Exists(testStr) Then
                    Return testStr
                Else
                    Return Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                End If
            Else
                'not a path
                Return testStr
            End If

        Catch ex As Exception

            'return original string contains invalid path characters
            Return testStr

        End Try

    End Function


End Class

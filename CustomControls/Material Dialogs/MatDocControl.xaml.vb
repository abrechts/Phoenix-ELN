Imports System.Globalization
Imports System.IO
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports ElnBase.ELNEnumerations
Imports ElnCoreModel
Imports Microsoft.Win32

Public Class MatDocControl

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    ''' <summary>
    ''' Sets or gets the maximum allowed individual document size in MB.
    ''' </summary>
    ''' 
    Public Shared Property MaxFileMB As Integer = 15


    ''' <summary>
    ''' Sets or gets the maximum number of documents per db material.
    ''' </summary>
    '''
    Public Shared Property MaxFileCount As Integer = 2


    Private Sub Me_DataContextChanged() Handles Me.DataContextChanged

        If Me.DataContext IsNot Nothing Then

            Dim docCount = CType(Me.DataContext, tblMaterials).tblDbMaterialFiles.Count

            If docCount > 0 Then
                cboDocs.Visibility = Visibility.Visible
                cboDocs.ItemsSource = CType(Me.DataContext, tblMaterials).tblDbMaterialFiles    
                cboDocs.SelectedIndex = 0
                If docCount = 1 Then
                    cboDocs.Cursor = Cursors.Hand
                Else
                    cboDocs.Cursor = Cursors.Arrow
                End If
            Else
                cboDocs.Visibility = Visibility.Collapsed
            End If

        End If

    End Sub


    Private Sub cboItem_DelClick()  'XAML defined event

    End Sub


    Private Sub matDocItem_PreviewMouseDown(sender As Object, e As RoutedEventArgs) 'XAML defined event

        Dim currDocEntry = CType(sender.DataContext, tblDbMaterialFiles)
        OpenDocument(currDocEntry)

        e.Handled = True

    End Sub


    Private Sub cboMat_PreviewMouseDown(sender As Object, e As MouseButtonEventArgs) Handles cboDocs.PreviewMouseDown

        Dim comboBox As ComboBox = TryCast(sender, ComboBox)
        Dim mousePosition As Point = e.GetPosition(comboBox)

        ' Assuming the dropdown arrow is on the right side and has a fixed width
        Dim arrowWidth As Double = SystemParameters.VerticalScrollBarWidth

        If mousePosition.X < comboBox.ActualWidth - arrowWidth Then
            ' Mouse click is outside the dropdown arrow area
            If CType(Me.DataContext, tblMaterials).tblDbMaterialFiles.Count = 1 Then
                OpenDocument(CType(cboDocs.SelectedItem, tblDbMaterialFiles))
                e.Handled = True
            End If
        End If

    End Sub


    Private Sub btnAdd_Click() Handles btnAdd.Click

        Dim matDbEntry = CType(Me.DataContext, tblMaterials)

        If matDbEntry.tblDbMaterialFiles.Count >= MaxFileCount Then
            MsgBox("Sorry, the maximum limit of " + MaxFileCount.ToString + " documents " + vbCrLf +
              "per material already is present!", MsgBoxStyle.OkOnly + MsgBoxStyle.Exclamation, "Documents Limit")
            Exit Sub
        End If

        Dim openFileDlg As New OpenFileDialog

        With openFileDlg

            If My.Settings.LastMatDocDir = "" Then
                .InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            Else
                .InitialDirectory = My.Settings.LastMatDocDir
            End If

            .Filter = "PDF document (*.pdf)|*.pdf"
            .Title = "Select a document to add ..."

            If .ShowDialog Then

                Dim res = EmbedDocument(.FileName)

                Select Case res
                    Case EmbeddingResult.SizeExceeded
                        MsgBox("File too large (" + MaxFileMB.ToString + " MB max)!", MsgBoxStyle.OkOnly + MsgBoxStyle.Exclamation, "File Size Limitation")
                        Exit Sub
                    Case EmbeddingResult.FileNotFound
                        'this return value is a synonym for duplicate entry here ...
                        MsgBox("Sorry, a document with the same name " + vbCrLf +
                          "is already present for this material!", MsgBoxStyle.OkOnly + MsgBoxStyle.Exclamation, "Duplicate Document")
                        Exit Sub
                    Case Else
                        'continue
                End Select

                With cboDocs
                    .ItemsSource = Nothing
                    .ItemsSource = matDbEntry.tblDbMaterialFiles
                    .SelectedIndex = .Items.Count - 1
                End With

                Dim docCount = matDbEntry.tblDbMaterialFiles.Count
                If docCount = 1 Then
                    cboDocs.Cursor = Cursors.Hand
                Else
                    cboDocs.Cursor = Cursors.Arrow
                End If

                My.Settings.LastMatDocDir = Path.GetDirectoryName(.FileName)

            End If

        End With

    End Sub


    Private Sub btnDelete_Click() Handles btnDelete.Click

        Dim currDoc As tblDbMaterialFiles = cboDocs.SelectedItem
        Dim matDbEntry = CType(Me.DataContext, tblMaterials)

        matDbEntry.tblDbMaterialFiles.Remove(currDoc)
        ExperimentContent.DbContext.SaveChanges()

        Dim docCount = matDbEntry.tblDbMaterialFiles.Count
        If docCount = 1 Then
            cboDocs.Cursor = Cursors.Hand
        Else
            cboDocs.Cursor = Cursors.Arrow
        End If

        With cboDocs
            .ItemsSource = Nothing
            .ItemsSource = matDbEntry.tblDbMaterialFiles
            .SelectedIndex = docCount - 1
        End With

    End Sub


    ''' <summary>
    ''' Creates a tmp file from the DB byte array and opens it in its default application.
    ''' </summary>
    ''' 
    Friend Sub OpenDocument(embeddedFilesEntry As tblDbMaterialFiles)

        With embeddedFilesEntry

            Dim fileName = .FileName
            fileName = String.Join("_", fileName.Split(Path.GetInvalidFileNameChars()))   'remove invalid chars
            Dim tempFilePath = TempDocsPath() + "\" + fileName

            Try
                'deserialize file to temp folder
                File.WriteAllBytes(tempFilePath, .FileBytes)

            Catch ex1 As Exception

                MsgBox("Can't open this document, since it's already " + vbCrLf +
                       "opened by another application.", MsgBoxStyle.Information, "Embedded Document")
                Exit Sub

            End Try

            Using docProcess = New Process
                With docProcess
                    .StartInfo.FileName = tempFilePath
                    .StartInfo.UseShellExecute = True
                    .Start()
                End With
            End Using

        End With

    End Sub


    ''' <summary>
    ''' Gets the path to the directory in the user's temp folder, in which temporary 
    ''' embedded docs created for viewing are stored.   
    ''' </summary>
    '''
    Public Shared Function TempDocsPath() As String

        If _tempDocsPath = "" Then
            _tempDocsPath = Path.GetTempPath + "PhoenixElnTempDocs"
            If Not Directory.Exists(_tempDocsPath) Then
                Directory.CreateDirectory(_tempDocsPath)
            End If
        End If
        Return _tempDocsPath

    End Function

    Private Shared _tempDocsPath As String


    ''' <summary>
    ''' Embeds the specified file, if the file size restrictions are met.
    ''' </summary>
    ''' 
    Private Function EmbedDocument(filePath As String) As EmbeddingResult

        Dim info = New IO.FileInfo(filePath)

        If info.Exists Then

            Dim currDbMat = CType(Me.DataContext, tblMaterials)

            'check for duplicate docs
            Dim fileName = Path.GetFileName(filePath)
            Dim res = From mat In currDbMat.tblDbMaterialFiles Where mat.FileName = fileName
            If res.Any Then
                Return EmbeddingResult.FileNotFound 'used as synonym for duplicate here ...
            End If

            'limit single file size
            Dim docMB = info.Length / (1024.0 * 1024.0)
            If docMB > MaxFileMB Then
                Return EmbeddingResult.SizeExceeded
            End If

            Dim newFileEntry = New tblDbMaterialFiles

            'get byte array of file, even if locked by another application
            Dim fileBytes = SerializeFile(filePath)
            With newFileEntry
                .GUID = Guid.NewGuid.ToString("D")
                .FileName = Path.GetFileName(filePath)
                .FileSizeMB = docMB
                .FileBytes = fileBytes
                .IconImage = FileContent.GetFileIconBytes(filePath)
            End With

            'save new file
            currDbMat.tblDbMaterialFiles.Add(newFileEntry)
            ExperimentContent.DbContext.SaveChanges()

            Return EmbeddingResult.Succeeded

        Else

            Return EmbeddingResult.FileNotFound

        End If

    End Function


    ''' <summary>
    ''' Serializes either a file or an image to be embedded, if embedding an image also reducing image size according to 
    ''' the specified maxPixels.
    ''' </summary>
    ''' <param name="filePath">Path to the file to be serialized.</param>
    ''' 
    Private Function SerializeFile(filePath As String) As Byte()

        Try

            Return File.ReadAllBytes(filePath)

        Catch ex As Exception

            'ReadAllBytes can't serialize locked files (e.g. open office docs). However, file copy works
            'just fine, so the file is copied to a temp file before serializing ... 

            Dim tmpPath = FileContent.TempDocsPath + "\copy.tmp"
            Dim res As Byte()

            File.Copy(filePath, tmpPath, True)
            res = File.ReadAllBytes(tmpPath)
            File.Delete(tmpPath)

            Return res

        End Try

    End Function

End Class

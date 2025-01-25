Imports System.Collections.ObjectModel
Imports System.Globalization
Imports System.IO
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Data
Imports System.Windows.Input
Imports ElnBase.ELNEnumerations
Imports ElnCoreModel
Imports Microsoft.Win32

Public Class MatDocControl

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        UpdateDropDownLayout()
        cboDocs_SelectionChanged()

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
    Public Shared Property MaxFileCount As Integer = 3


    ''' <summary>
    ''' Sets or gets the maximum width of the documents ComboBox (default=160)
    ''' </summary>
    '''
    Public Property MaxComboBoxWidth As Double
        Get
            Return cboDocs.MaxWidth
        End Get
        Set(value As Double)
            cboDocs.MaxWidth = value
        End Set
    End Property


    ''' <summary>
    ''' Sets or gets if the current protocol material entry is not yet assigned to a specific db material.
    ''' </summary>
    ''' 
    Public Property Documents As ObservableCollection(Of tblDbMaterialFiles)
        Get
            Return _documents
        End Get
        Set(value As ObservableCollection(Of tblDbMaterialFiles))
            _documents = value
            cboDocs.ItemsSource = value
            SetAppearance()
        End Set
    End Property

    Private _documents As ObservableCollection(Of tblDbMaterialFiles)


    ''' <summary>
    ''' Gets the index of the currently selected material safety doc. Returns nothing if no doc is present.
    ''' </summary>
    ''' 
    Public ReadOnly Property SelectedDocIndex As Integer?
        Get
            If cboDocs.SelectedIndex >= 0 Then
                Return cboDocs.SelectedIndex
            Else
                Return Nothing
            End If
        End Get
    End Property


    Private Sub SetAppearance()

        If Documents.Count > 0 Then
            With cboDocs
                .Visibility = Visibility.Visible
                .SelectedIndex = If(Documents.First.DbMaterial.CurrDocIndex, 0)
            End With
            btnAdd.IsEnabled = True
            btnAdd.Opacity = 1
            If Documents.Count = 1 Then
                cboDocs.Cursor = Cursors.Hand
            Else
                cboDocs.Cursor = Cursors.Arrow
            End If
        Else
            cboDocs.Visibility = Visibility.Collapsed
        End If

    End Sub


    ''' <summary>
    ''' Workaround to allow tooltip for truncated text in ComboBoxItem in so far unrendered ComboBox 
    '''' dropdown. 
    ''' </summary>
    ''' 
    Private Sub UpdateDropDownLayout()

        WPFToolbox.WaitForPriority(Threading.DispatcherPriority.ContextIdle)
        With cboDocs
            .IsDropDownOpen = True
            .IsDropDownOpen = False
        End With

    End Sub


    ''' <summary>
    ''' Displays fileName ToolTip on ComboBox if its SelectedItem is truncated.
    ''' </summary>
    ''' 
    Private Sub cboDocs_SelectionChanged() Handles cboDocs.SelectionChanged

        Dim cboItem As ComboBoxItem = cboDocs.ItemContainerGenerator.ContainerFromItem(cboDocs.SelectedItem)
        If cboItem IsNot Nothing Then

            Dim cboItemDesiredWidth = cboItem.DesiredSize.Width
            Dim cboMaxWidth = cboDocs.MaxWidth

            If cboMaxWidth < cboItemDesiredWidth Then
                Dim fileName = CType(cboDocs.SelectedItem, tblDbMaterialFiles).FileName
                cboDocs.ToolTip = Path.GetFileNameWithoutExtension(fileName)
            Else
                cboDocs.ToolTip = Nothing
            End If

        End If

    End Sub


    Private Sub matDocItem_PreviewMouseLeftButtonUp(sender As Object, e As RoutedEventArgs) 'XAML defined event

        Dim currDocEntry = CType(sender.DataContext, tblDbMaterialFiles)
        OpenDocument(currDocEntry)

    End Sub


    Private Sub cboMat_PreviewMouseDown(sender As Object, e As MouseButtonEventArgs) Handles cboDocs.PreviewMouseDown

        'If only one doc present, opens it on mouse click on cboDocs directly and without dropdown.

        If cboDocs.IsDropDownOpen Then
            Exit Sub
        End If

        Dim mousePosition As Point = e.GetPosition(cboDocs)

        ' Assuming the dropdown arrow is on the right side and has a fixed width
        Dim arrowWidth As Double = SystemParameters.VerticalScrollBarWidth

        If mousePosition.X < cboDocs.ActualWidth - arrowWidth Then
            ' Mouse click is outside the dropdown arrow area
            If Documents.Count = 1 Then
                OpenDocument(CType(cboDocs.SelectedItem, tblDbMaterialFiles))
                e.Handled = True
            End If
        End If

    End Sub


    Private Sub btnAdd_Click() Handles btnAdd.PreviewMouseUp

        If Documents.Count >= MaxFileCount Then
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
                        MsgBox("File too large (" + MaxFileMB.ToString + " MB max)!", MsgBoxStyle.OkOnly + MsgBoxStyle.Exclamation,
                               "File Size Limitation")
                        Exit Sub
                    Case EmbeddingResult.FileNotFound
                        'this return value is a synonym for duplicate entry here ...
                        MsgBox("Sorry, a document with the same name " + vbCrLf +
                                "is already present for this material!", MsgBoxStyle.OkOnly + MsgBoxStyle.Exclamation,
                                "Duplicate Document")
                        Exit Sub
                    Case Else
                        'continue
                End Select

                UpdateDropDownLayout()
                cboDocs.SelectedIndex = Documents.Count - 1

                Dim docCount = Documents.Count ' matDbEntry.tblDbMaterialFiles.Count
                If docCount = 1 Then
                    cboDocs.Cursor = Cursors.Hand
                Else
                    cboDocs.Cursor = Cursors.Arrow
                End If
                cboDocs.Visibility = If(docCount = 0, Visibility.Collapsed, Visibility.Visible)

                My.Settings.LastMatDocDir = Path.GetDirectoryName(.FileName)

            End If

        End With

    End Sub


    Private Sub cboItem_DelClick(sender As Object, e As MouseButtonEventArgs) 'XAML specified event

        Dim res = MsgBox("Do you really want to remove this document?", MsgBoxStyle.OkCancel + MsgBoxStyle.DefaultButton2 + MsgBoxStyle.Question,
          "Delete Document")
        If res = MsgBoxResult.Cancel Then
            Exit Sub
        End If

        Dim currDoc As tblDbMaterialFiles = sender.DataContext
        Documents.Remove(currDoc)

        Dim docCount = Documents.Count
        If docCount = 1 Then
            cboDocs.Cursor = Cursors.Hand
        Else
            cboDocs.Cursor = Cursors.Arrow
        End If
        cboDocs.Visibility = If(docCount = 0, Visibility.Collapsed, Visibility.Visible)

        cboDocs.SelectedIndex = Documents.Count - 1

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

            Dim fileName = Path.GetFileName(filePath)
            Dim res = From doc In Documents Where doc.FileName = fileName
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
                .FileName = WPFToolbox.ShortenFileNameWithExtension(Path.GetFileName(filePath), 80) 'shorten file name if required, keeping extension
                .FileSizeMB = docMB
                .FileBytes = fileBytes
                .IconImage = FileContent.GetFileIconBytes(filePath)
            End With

            Documents.Add(newFileEntry)

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


Friend Class FileNameConverter

    Implements IValueConverter

    ''' <summary>
    ''' Removes the file name extension
    ''' </summary>
    ''' 
    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim fullName As String = value
        Return Path.GetFileNameWithoutExtension(fullName)

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function

End Class











Imports System.IO
Imports System.Windows.Media.Imaging
Imports ElnCoreModel
Imports Microsoft.Win32

Public Class ImageContent

    Public Shared Event MouseOverChanged(sender As Object, isEntering As Boolean)

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    Public Sub ActivateEdit()

        txtComments.Focus()
        txtComments.Select(0, 255)

    End Sub


    Private Sub displayImg_MouseEnter() Handles displayImg.MouseEnter
        RaiseEvent MouseOverChanged(Me, True)
    End Sub


    Private Sub displayImg_MouseLeave() Handles displayImg.MouseLeave
        RaiseEvent MouseOverChanged(Me, False)
    End Sub


    Private Sub blkRotate_MouseUp() Handles icoRotate.MouseUp

        Dim fileEntry = CType(Me.DataContext, tblEmbeddedFiles)

        If fileEntry.IsRotated = 0 Then
            fileEntry.IsRotated = 1
        Else
            fileEntry.IsRotated = 0
        End If

        WPFToolbox.FindVisualParent(Of Protocol)(Me).AutoSave()

    End Sub


    Private Sub mnuFullScreen_Click() Handles mnuExpand.Click

        displayImg_MouseLeftButtonUp()

    End Sub

    Private Sub mnuSaveAs_Click() Handles mnuSaveAs.Click

        ExportImage()

    End Sub


    ''' <summary>
    ''' Displays a SaveFile dialog for exporting the embedded image to a file location. The image format dropdown menu 
    ''' allows to export the image in JPEG, PNG or TIF format. If the original image was stored in one 
    ''' of these formats, this format will be selected by default. Otherwise JPEG is selected.
    ''' </summary>
    ''' <remarks>
    ''' Originally uncompressed TIF files are exported to TIF with the best suited lossless compression (LZW or ZIP).
    ''' </remarks>
    ''' 
    Private Sub ExportImage()

        Dim saveDialog As New SaveFileDialog
        With saveDialog

            Dim fileEntry = CType(Me.DataContext, tblEmbeddedFiles)
            Dim fileName = Path.GetFileNameWithoutExtension(fileEntry.FileName)
            Dim fileExtension = Path.GetExtension(fileEntry.FileName)

            .InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            .Title = "Export embedded image as file"
            .Filter = "JPEG format|*.jpg|PNG format|*.png|TIFF format|*.tif"

            .FileName = fileName

            Select Case fileExtension
                Case ".tif"
                    .FilterIndex = 3
                Case ".png"
                    .FilterIndex = 2
                Case Else
                    .FilterIndex = 1
            End Select

            If .ShowDialog Then

                Using stream As New MemoryStream(fileEntry.FileBytes)

                    stream.Seek(0, SeekOrigin.Begin)

                    Dim bmImage As New BitmapImage()
                    With bmImage
                        .BeginInit()
                        .CacheOption = BitmapCacheOption.OnLoad
                        .StreamSource = stream
                        .EndInit()
                    End With

                    Dim currEncoder As Object

                    Select Case .FilterIndex
                        Case 3
                            currEncoder = New TiffBitmapEncoder()
                        Case 2
                            currEncoder = New PngBitmapEncoder()
                        Case Else
                            currEncoder = New JpegBitmapEncoder()
                    End Select

                    currEncoder.Frames.Add(BitmapFrame.Create(bmImage))

                    'save image file
                    Using fs = New FileStream(.FileName, FileMode.Create)
                        currEncoder.Save(fs)
                    End Using

                    'display image file in default viewer
                    Try
                        Dim info As New ProcessStartInfo(.FileName)
                        info.UseShellExecute = True
                        Process.Start(info)
                    Catch ex As Exception
                        'just skip if no default browser available
                    End Try

                End Using
            End If
        End With

    End Sub


    Private Sub displayImg_MouseLeftButtonUp() Handles displayImg.MouseLeftButtonUp

        Dim fullImageDlg As New dlgFullImage
        With fullImageDlg
            .DataContext = Me.DataContext
            .ShowDialog()
        End With

    End Sub


    Private Sub txtComments_LostFocus() Handles Me.LostFocus

        With txtComments
            If .CanUndo Then
                .IsUndoEnabled = False  'clears the undo stack
                .IsUndoEnabled = True   'restarts it again
                Dim myProtocol = WPFToolbox.FindVisualParent(Of Protocol)(Me)
                myProtocol?.AutoSave()
            End If
        End With

    End Sub


End Class

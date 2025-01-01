Imports ElnBase.ELNEnumerations
Imports ElnCoreModel
Imports Microsoft.Win32
Imports Spire.Pdf
Imports System.Drawing.Printing
Imports System.Globalization
Imports System.IO
Imports System.IO.Packaging
Imports System.Threading
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Documents
Imports System.Windows.Input

Public Class ExperimentPrint

    ''' <summary>
    ''' Prints the current experiment to the desired printer or to a PDF document, 
    ''' as specified by the printAsPDF parameter.
    ''' </summary>
    ''' <param name="appWindow">The application window. Required for displaying a global Wait cursor while processing.</param>
    ''' 
    Public Shared Sub Print(expContent As ExperimentContent, printAsPDF As Boolean, appWindow As Window)

        Dim expEntry = expContent.DataContext
        Dim origSketchArea = expContent.SketchPanel
        Dim pdfPath As String = ""

        If printAsPDF Then

            Dim saveFileDlg As New SaveFileDialog
            With saveFileDlg
                If My.Settings.LastPdfSaveDir = "" Then
                    .InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                Else
                    .InitialDirectory = My.Settings.LastPdfSaveDir
                End If
                .Filter = "PDF file |*.pdf"
                .FileName = expEntry.ExperimentID + ".pdf"
                .Title = "Save experiment as PDF"
                If .ShowDialog Then
                    pdfPath = .FileName
                    My.Settings.LastPdfSaveDir = Path.GetDirectoryName(pdfPath)
                Else
                    Exit Sub
                End If
            End With

        End If

        'set wait cursor on application window
        appWindow.Cursor = Cursors.Wait
        appWindow.ForceCursor = True
        WPFToolbox.WaitForPriority(Threading.DispatcherPriority.ContextIdle)

        Dim printExpContent As New ExperimentContent
        printExpContent.DataContext = expEntry

        WPFToolbox.WaitForPriority(Threading.DispatcherPriority.ContextIdle)

        Dim printStack As New StackPanel
        printStack.Children.Add(printExpContent)
        printExpContent.SetPrintUI()

        With printExpContent
            .Measure(New Size)
            .Arrange(New Rect)
            .SketchPanel.SetComponentLabels(origSketchArea.ComponentFontSize, origSketchArea.BottomOffset)
        End With


        Dim printDoc = New PrintDocument()
        Dim paperSize = printDoc.DefaultPageSettings.PaperSize
        Dim stackPrintTempl As New PrintPageTemplate(printStack, paperSize, 0.95)

        With stackPrintTempl
            .ShowConfidentialMarker = False
            .IsHeaderVisible = False
            .FooterTitleLeft = expEntry.User.CompanyName
            .FooterTitleRight = expEntry.ExperimentID
            .FooterCenterImage = Nothing ' --> add CompanyLogoImage in the future
        End With

        Try

            If printAsPDF Then

                Dim pdfDoc = PaginatorToPdfDoc(stackPrintTempl.Paginator, printAsPDF)
                If pdfDoc IsNot Nothing Then
                    CompletePDF(pdfDoc, pdfPath, expEntry)  'add attachments, convert to PDF/A-3b
                End If

                Dim info As New ProcessStartInfo(pdfPath)
                info.UseShellExecute = True
                Process.Start(info)

            Else

                Dim printDlg As New PrintDialog
                If Not printDlg.ShowDialog Then
                    Exit Sub
                End If

                printDlg.PrintDocument(stackPrintTempl.Paginator, "Printing " + expEntry.ExperimentID)

            End If

        Catch ex As Exception

            If printAsPDF Then
                MsgBox("Could not overwrite the specified file, since it" + vbCrLf +
                "is currently in use by another application!", MsgBoxStyle.Information + MsgBoxStyle.OkOnly, "PDF Creation")
            End If

        Finally

            appWindow.Cursor = Cursors.Arrow
            appWindow.ForceCursor = False

        End Try

    End Sub


    ''' <summary>
    ''' Enriches initial origPDF (as created from XPS doc) with embedded file attachments, and convert it to PDF/A-3b.  
    ''' </summary>
    ''' <param name="origPDF">The initial PDF document to convert.</param>
    ''' <param name="convPDFPath">The destination path to save it to.</param>
    ''' 
    Private Shared Sub CompletePDF(origPDF As PdfDocument, convPDFPath As String, expEntry As tblExperiments)

        With origPDF

            'attach embedded documents
            '-------------------------

            Dim embeddedFiles = From prot In expEntry.tblProtocolItems Where prot.ElementType = ProtocolElementType.File
                                Select prot.tblEmbeddedFiles

            For Each embedded In embeddedFiles
                AttachEmbeddedDoc(origPDF, embedded.FileName, embedded.FileComment, embedded.FileBytes)
            Next

            Dim convertedPDF As PdfDocument
            Try
                'try convert to PDF/A3b
                '-----------------------
                Using pdfStream As New IO.MemoryStream
                    .SaveToStream(pdfStream)
                    Using outStream As New IO.MemoryStream
                        Using conv As New Conversion.PdfStandardsConverter(pdfStream)
                            conv.ToPdfA3B(outStream)     'breaks only in debug mode, is otherwise caught by catch
                            convertedPDF = New PdfDocument(outStream)
                        End Using
                    End Using
                End Using

            Catch ex As Exception

                'non-PDF/A-3b version
                convertedPDF = origPDF
                MsgBox("Unable to save your document in PDF/A3b format." + vbCrLf +
                       "It will be saved as standard PDF instead.", MsgBoxStyle.Information, "PDF Export")
            End Try

            'define its display settings
            With .ViewerPreferences
                If convertedPDF.Attachments.Count > 0 Then
                    .PageMode = PdfPageMode.UseAttachments
                ElseIf convertedPDF.Pages.Count > 1 Then
                    .PageMode = PdfPageMode.UseOutlines
                End If
                .PageLayout = PdfPageLayout.SinglePage
            End With

            'save document
            Try
                convertedPDF.SaveToFile(convPDFPath)
            Catch ex As Exception
                MsgBox("Could Not write PDF, since a document with the same" + vbCrLf +
               "name is currently open in a PDF viewer. ", MsgBoxStyle.Information, "PDF Export")
                Exit Sub
            End Try

        End With

    End Sub


    ''' <summary>
    ''' Attaches the specified files to the specified PDF document.
    ''' </summary>
    ''' 
    Private Shared Sub AttachEmbeddedDoc(pdfDoc As PdfDocument, fileTitle As String, fileComment As String, fileBytes As Byte())

        If fileBytes IsNot Nothing Then
            Dim attachDoc As New Attachments.PdfAttachment(fileTitle, fileBytes)
            attachDoc.Description = fileComment
            pdfDoc.Attachments.Add(attachDoc)
        End If

    End Sub


    ''' <summary>
    ''' Converts a DocumentPaginator to a Spire.PDFDocument
    ''' </summary>
    ''' <param name="docPaginator">The paginator to convert.</param>
    ''' <returns>The resulting Spire.PdfDocument</returns>
    '''
    Private Shared Function PaginatorToPdfDoc(docPaginator As DocumentPaginator, printAsPDF As Boolean) As PdfDocument

        Using xpsMemStream = New MemoryStream

            Dim myPackage = Package.Open(xpsMemStream, FileMode.Create, FileAccess.ReadWrite)
            Dim u = New Uri("pack://TemporaryPackageUri.pdf")
            Dim pdfXPSDoc = New Xps.Packaging.XpsDocument(myPackage, CompressionOption.NotCompressed, u.AbsoluteUri)
            Dim paginator2PDFWriter = Xps.Packaging.XpsDocument.CreateXpsDocumentWriter(pdfXPSDoc)
            paginator2PDFWriter.Write(docPaginator)

            pdfXPSDoc.Close()
            myPackage.Close()

            'check for Spire.Pdf Free 10 page limit
            If printAsPDF AndAlso docPaginator.PageCount > 10 Then
                MsgBox("Sorry, can't create PDF documents larger " + vbCrLf +
                       "than 10 pages.", MsgBoxStyle.Information, "PDF limits reached.")
                Return Nothing
            End If

            'workaround for decimal separator issue
            Dim cc = Thread.CurrentThread.CurrentCulture
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture

            Dim myDoc As New PdfDocument
            myDoc.LoadFromXPS(xpsMemStream)

            Thread.CurrentThread.CurrentCulture = cc

            Return myDoc

        End Using

    End Function


End Class



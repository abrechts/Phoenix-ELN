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
        Dim paperSize As PaperSize

        If printAsPDF Then

            ' PDF export

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

            appWindow.Cursor = Cursors.Wait
            appWindow.ForceCursor = True
            WPFToolbox.WaitForPriority(Threading.DispatcherPriority.ContextIdle)

            Dim printDoc = New PrintDocument()
            If printDoc.PrinterSettings.IsValid Then
                paperSize = printDoc.DefaultPageSettings.PaperSize
            Else
                'fallback to A4 if no printer is installed
                paperSize = New PaperSize("A4", 827, 1169)
            End If

            Dim printTemplate = SetPrintTemplate(expEntry, origSketchArea, paperSize)

            ' create and store PDF doc with attachments
            Dim pdfDoc = PaginatorToPdfDoc(printTemplate.Paginator, printAsPDF)
            If pdfDoc IsNot Nothing Then
                If CompletePDF(pdfDoc, pdfPath, expEntry) Then 'add attachments, convert to PDF/A-3b; also handles file open exception with dialog.
                    Dim info As New ProcessStartInfo(pdfPath)
                    info.UseShellExecute = True
                    Process.Start(info)
                End If
            End If

            appWindow.Cursor = Cursors.Arrow
            appWindow.ForceCursor = False

        Else

            ' Printer

            Dim printDlg As New PrintDialog
            If Not printDlg.ShowDialog Then
                Exit Sub
            End If

            With printDlg.PrintTicket.PageMediaSize
                Dim height = CInt(.Height * 100 / 96)
                Dim width = CInt(.Width * 100 / 96)
                paperSize = New PaperSize(.PageMediaSizeName.ToString(), width, height)
            End With

            Dim printTemplate = SetPrintTemplate(expEntry, origSketchArea, paperSize)

            Try
                printDlg.PrintDocument(printTemplate.Paginator, "Printing " + expEntry.ExperimentID)
            Catch ex As Exception
                MsgBox("Printing error. If using a printer driver generating an" + vbCrLf +
                       "output file (e.g. PDF), the specified file may currently" + vbCrLf +
                       "be in use by another application and can't be overwritten!",
                       MsgBoxStyle.Information + MsgBoxStyle.OkOnly, "Printing Error")

            End Try

        End If

    End Sub


    ''' <summary>
    ''' Creates and returns the PrintPageTemplate for the given experiment entry and sketch area.
    ''' </summary>
    ''' 
    Private Shared Function SetPrintTemplate(expEntry As tblExperiments, skArea As SketchArea, pageSize As PaperSize) As PrintPageTemplate

        Dim printExpContent As New ExperimentContent
        printExpContent.Width = 680     'current UI display width of this data template
        printExpContent.DataContext = expEntry

        WPFToolbox.WaitForPriority(Threading.DispatcherPriority.ContextIdle)

        Dim printStack As New StackPanel
        printStack.Children.Add(printExpContent)
        printExpContent.SetPrintUI()

        With printExpContent
            .Measure(New Size)
            .Arrange(New Rect)
            .SketchPanel.SetComponentLabels(skArea.ComponentFontSize, skArea.BottomOffset)
        End With

        Dim stackPrintTempl As New PrintPageTemplate(printStack, pageSize, 0.95)

        With stackPrintTempl
            .ShowConfidentialMarker = False
            .IsHeaderVisible = False
            .FooterTitleLeft = expEntry.User.CompanyName
            .FooterTitleRight = expEntry.ExperimentID
            .FooterCenterImage = Nothing ' --> add CompanyLogoImage in the future
        End With

        Return stackPrintTempl

    End Function


    ''' <summary>
    ''' Enriches initial origPDF (as created from XPS doc) with embedded file attachments, and convert it to PDF/A-3b.  
    ''' </summary>
    ''' <param name="origPDF">The initial PDF document to convert.</param>
    ''' <param name="convPDFPath">The destination path to save it to.</param>
    ''' 
    Private Shared Function CompletePDF(origPDF As PdfDocument, convPDFPath As String, expEntry As tblExperiments) As Boolean

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
                MsgBox("Could not write PDF, since a document with the same" + vbCrLf +
               "name Is currently open in a PDF viewer. ", MsgBoxStyle.Information, "PDF Export")
                Return False
            End Try

            Return True

        End With

    End Function


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



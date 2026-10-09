Imports System.Drawing.Printing
Imports System.Globalization
Imports System.IO
Imports System.IO.Compression
Imports System.IO.Packaging
Imports System.Threading
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports ElnBase.ELNEnumerations
Imports ElnCoreModel
Imports Microsoft.Win32
Imports Spire.Pdf

Public Class ExperimentPrint


    Public Shared Event PdfExportProgress(percent As Integer, pdfTitle As String)

    Public Shared Event PDFExportCompleted(success As Boolean)


    ''' <summary>
    ''' Sets of gets if the bulk PDF export was cancelled
    ''' </summary>
    ''' 
    Public Shared Property ExportCancelled As Boolean = False


    ''' <summary>
    ''' An experiment the bulk PDF export could not render, with the reason it was left out.
    ''' </summary>
    '''
    Public Class SkippedExperiment

        Public Property ExperimentID As String

        Public Property Reason As PdfRenderResult

    End Class


    ''' <summary>
    ''' The experiments that the last bulk PDF export could not render, and therefore left out of the zip
    ''' archive. Populated by ExperimentsToPdfZip; the caller reports them once the export has finished,
    ''' rather than interrupting the run with a dialog per item.
    ''' </summary>
    '''
    Public Shared Property SkippedExperiments As New List(Of SkippedExperiment)


    ''' <summary>
    ''' Maximum number of pages a PDF may have. Imposed by the Spire.Pdf free edition, not by Phoenix.
    ''' </summary>
    '''
    Private Const MaxPdfPageCount As Integer = 10


    ''' <summary>
    ''' Prints the current experiment to the desired printer or to a PDF document, 
    ''' as specified by the printAsPDF parameter.
    ''' </summary>
    ''' <param name="appWindow">The application window. Required for displaying a global Wait cursor while processing.</param>
    ''' 
    Public Shared Sub Print(expContent As ExperimentContent, printAsPDF As Boolean, appWindow As Window)

        Dim expEntry = expContent.DataContext
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

            Dim printTemplate = SetPrintTemplate(expEntry, paperSize)

            ' create and store PDF doc with attachments
            Dim renderResult As PdfRenderResult
            Dim pdfDoc = PaginatorToPdfDoc(printTemplate.Paginator, printAsPDF, renderResult)
            If pdfDoc IsNot Nothing Then
                renderResult = CompletePDF(pdfDoc, pdfPath, expEntry)   'add attachments, convert to PDF/A-3b
            End If

            DisplayRenderResult(renderResult)

            If renderResult = PdfRenderResult.Succeeded OrElse renderResult = PdfRenderResult.SucceededWithoutPdfA Then
                Dim info As New ProcessStartInfo(pdfPath)
                info.UseShellExecute = True
                Process.Start(info)
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

            Dim printTemplate = SetPrintTemplate(expEntry, paperSize)

            Try
                printDlg.PrintDocument(printTemplate.Paginator, "Printing " + expEntry.ExperimentID)
            Catch ex As Exception
                cbMsgBox.Display("Unable to print. If using a printer driver generating an" + vbCrLf +
                       "output file (e.g. PDF), the specified file may currently" + vbCrLf +
                       "be in use by another application and can't be overwritten!",
                       MsgBoxStyle.Information + MsgBoxStyle.Exclamation, "Print Error")

            End Try

        End If

    End Sub


    ''' <summary>
    ''' Converts the specified list of experiment entries into PDF documents and combines them into a zip folder.
    ''' </summary>
    ''' <param name="expEntries">A list of tblExperiments entries to be part of the zip folder.</param>
    ''' <param name="zipFolderPath">The full path of the destination zip archive.</param>
    ''' <returns>True, if successful</returns>
    ''' 
    Public Shared Function ExperimentsToPdfZip(expEntries As List(Of tblExperiments), zipFolderPath As String) As Boolean

        ExperimentsToPdfZip = False
        ExportCancelled = False
        SkippedExperiments = New List(Of SkippedExperiment)

        If expEntries IsNot Nothing Then

            ' get current paper size

            Dim printDoc = New PrintDocument()
            Dim paperSize As PaperSize
            If printDoc.PrinterSettings.IsValid Then
                paperSize = printDoc.DefaultPageSettings.PaperSize
            Else
                paperSize = New PaperSize("A4", 827, 1169) 'fall back to A4 if no printer is installed
            End If

            ' create temporary pdf folder

            Dim tempPdfFolderPath = Path.Combine(Path.GetTempPath(), "tmpPDF_" & Guid.NewGuid().ToString())
            Directory.CreateDirectory(tempPdfFolderPath)

            ' create PDFs in temp folder


            Dim expCount = expEntries.Count

            For Each expEntry In expEntries

                If Not ExportCancelled Then

                    Dim expPdfPath = tempPdfFolderPath + "\" + expEntry.ExperimentID + ".pdf"
                    Dim printTemplate = SetPrintTemplate(expEntry, paperSize)

                    Dim renderResult As PdfRenderResult
                    Dim pdfDoc = PaginatorToPdfDoc(printTemplate.Paginator, True, renderResult)
                    If pdfDoc IsNot Nothing Then
                        renderResult = CompletePDF(pdfDoc, expPdfPath, expEntry) 'add attachments, convert to PDF/A-3b
                    End If

                    If renderResult = PdfRenderResult.Succeeded OrElse renderResult = PdfRenderResult.SucceededWithoutPdfA Then
                        Dim percent As Integer = 100 * (expEntries.IndexOf(expEntry) + 1) / expCount
                        RaiseEvent PdfExportProgress(percent, expEntry.ExperimentID)
                    Else
                        'no dialog here: one modal per item would interrupt the run repeatedly, and the
                        'caller reports the whole list once the export finishes
                        SkippedExperiments.Add(New SkippedExperiment With {
                            .ExperimentID = expEntry.ExperimentID, .Reason = renderResult})
                    End If

                Else

                    ' export was cancelled by user
                    Directory.Delete(tempPdfFolderPath, True)
                    Return False

                End If

            Next


            ' create zip file

            Try
                If File.Exists(zipFolderPath) Then
                    File.Delete(zipFolderPath)  'same as overwrite existing archive
                End If
                ZipFile.CreateFromDirectory(tempPdfFolderPath, zipFolderPath)
                ExperimentsToPdfZip = True
            Catch ex As Exception
                'do nothing
            Finally
                ' delete temp folder
                Try
                    RaiseEvent PDFExportCompleted(ExperimentsToPdfZip)
                    Directory.Delete(tempPdfFolderPath, True)
                Catch ex As Exception
                    'do nothing
                End Try
            End Try

        End If

    End Function


    ''' <summary>
    ''' Creates and returns the PrintPageTemplate for the given experiment entry and sketch area.
    ''' </summary>
    ''' 
    Private Shared Function SetPrintTemplate(expEntry As tblExperiments, pageSize As PaperSize) As PrintPageTemplate

        Dim printExpContent As New ExperimentContent With {
            .Width = 680,     'current UI display width of this data template
            .DataContext = expEntry
        }

        'this tree is detached, so its sketch area must not broadcast on the shared SketchSourceChanged
        'event: the live subscribers would adopt the rendered experiment's sketch info, and off the UI
        'thread they would touch DependencyObjects they do not own. Must be set before the assignment below.
        printExpContent.SketchPanel.IsDetachedRender = True

        'force the reaction sketch to draw immediately: since printExpContent is never attached to a
        'PresentationSource (it's only ever serialized to XPS/PDF, never shown), the ReactionSketch
        'binding's target update never fires, so sketchViewbox.Child would still be Nothing below.
        printExpContent.SketchPanel.ReactionSketch = expEntry.RxnSketch

        WPFToolbox.WaitForPriority(Threading.DispatcherPriority.ContextIdle)

        Dim printStack As New StackPanel
        printStack.Children.Add(printExpContent)
        printExpContent.SetPrintUI()

        With printExpContent
            .Measure(New Size)
            .Arrange(New Rect)
            .SketchPanel.SetComponentLabels()
        End With

        Dim stackPrintTemplate As New PrintPageTemplate(printStack, pageSize, 0.95)

        With stackPrintTemplate
            .ShowConfidentialMarker = False
            .IsHeaderVisible = False
            .FooterTitleLeft = expEntry.User.CompanyName
            .FooterTitleRight = expEntry.ExperimentID
            .FooterCenterImage = Nothing ' --> add CompanyLogoImage in the future
        End With

        Return stackPrintTemplate

    End Function


    ''' <summary>
    ''' Displays the message belonging to an unsuccessful PdfRenderResult, if any. Must be called on the
    ''' UI thread: the render functions themselves only report outcomes, so that they stay usable from a
    ''' background render.
    ''' </summary>
    '''
    ''' <summary>
    ''' Short phrase naming why a render did not succeed, for listing several failures together.
    ''' Capitalized, since it always starts its own line.
    ''' </summary>
    '''
    Public Shared Function RenderResultText(result As PdfRenderResult) As String

        Select Case result

            Case PdfRenderResult.PageLimitExceeded
                Return "More than " + MaxPdfPageCount.ToString + " pages"

            Case PdfRenderResult.DestinationFileLocked
                Return "File already in use"

            Case Else
                Return result.ToString

        End Select

    End Function


    Public Shared Sub DisplayRenderResult(result As PdfRenderResult)

        Select Case result

            Case PdfRenderResult.PageLimitExceeded
                cbMsgBox.Display("Sorry, can't create PDF documents larger " + vbCrLf +
                       "than " + MaxPdfPageCount.ToString + " pages.", MsgBoxStyle.Information, "PDF limits reached.")

            Case PdfRenderResult.SucceededWithoutPdfA
                cbMsgBox.Display("Unable to save your document in PDF/A3b format." + vbCrLf +
                       "It will be saved as standard PDF instead.", MsgBoxStyle.Information, "PDF Export")

            Case PdfRenderResult.DestinationFileLocked
                cbMsgBox.Display("Could not write PDF, since a document with the same" + vbCrLf +
                       "name is currently open in a PDF viewer. ", MsgBoxStyle.Exclamation, "PDF Export")

        End Select

    End Sub


    ''' <summary>
    ''' Enriches initial origPDF (as created from XPS doc) with embedded file attachments, and convert it to PDF/A-3b.
    ''' </summary>
    ''' <remarks> PDF/A-3b conformity: Following online tools can be used to check the resulting document:
    '''    https://www.pdfforge.org/online/de/pdfa-validieren
    '''    https://www.pdf2go.com/de/pdfa-validieren 
    '''    Use non-confidential PDFs only for upload!
    ''' </remarks>
    ''' <param name="origPDF">The initial PDF document to convert.</param>
    ''' <param name="convPDFPath">The destination path to save it to.</param>
    ''' <returns>The render outcome. Failing the PDF/A-3b conversion still writes the document, as a
    ''' standard PDF, and is reported as SucceededWithoutPdfA.</returns>
    '''
    Private Shared Function CompletePDF(origPDF As PdfDocument, convPDFPath As String,
      expEntry As tblExperiments) As PdfRenderResult

        With origPDF

            'attach embedded documents
            '-------------------------

            Dim embeddedFiles = From prot In expEntry.tblProtocolItems Where prot.ElementType = ProtocolElementType.File
                                Select prot.tblEmbeddedFiles

            For Each embedded In embeddedFiles
                AttachEmbeddedDoc(origPDF, embedded.FileName, embedded.FileComment, embedded.FileBytes)
            Next

            Dim convertedPDF As PdfDocument
            Dim outcome = PdfRenderResult.Succeeded
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
                outcome = PdfRenderResult.SucceededWithoutPdfA
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
                Return PdfRenderResult.DestinationFileLocked
            End Try

            Return outcome

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
    ''' <summary>
    ''' Serializes the paginated visual to XPS and loads it into a PdfDocument.
    ''' </summary>
    ''' <param name="enforcePageLimit">Applies the Spire.Pdf free 10 page limit. Not applied when the
    ''' destination is a printer, where Spire is not involved.</param>
    ''' <param name="result">Receives the outcome. Nothing is returned unless this is Succeeded.</param>
    '''
    Private Shared Function PaginatorToPdfDoc(docPaginator As PrintPageTemplate.VisualPaginator,
      enforcePageLimit As Boolean, ByRef result As PdfRenderResult) As PdfDocument

        Using xpsMemStream = New MemoryStream

            'The package URI is the key under which WPF registers this package in the process-global
            'PackageStore, so it must be unique per call: a shared URI collides as soon as two renders
            'overlap, and because the registration is only released on Close, a render that throws before
            'closing would otherwise poison every later render in the process.
            Dim u = New Uri("pack://render" + Guid.NewGuid().ToString("N") + ".pdf")

            Dim myPackage = Package.Open(xpsMemStream, FileMode.Create, FileAccess.ReadWrite)
            Try
                Dim pdfXPSDoc = New Xps.Packaging.XpsDocument(myPackage, CompressionOption.NotCompressed, u.AbsoluteUri)
                Try
                    Dim paginator2PDFWriter = Xps.Packaging.XpsDocument.CreateXpsDocumentWriter(pdfXPSDoc)
                    paginator2PDFWriter.Write(docPaginator)
                Finally
                    pdfXPSDoc.Close()
                End Try
            Finally
                myPackage.Close()
            End Try

            'check for Spire.Pdf Free 10 page limit
            If enforcePageLimit AndAlso docPaginator.PageCount > MaxPdfPageCount Then
                result = PdfRenderResult.PageLimitExceeded
                Return Nothing
            End If

            'workaround for decimal separator issue
            Dim cc = Thread.CurrentThread.CurrentCulture
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture

            Dim myDoc As New PdfDocument
            myDoc.LoadFromXPS(xpsMemStream)

            Thread.CurrentThread.CurrentCulture = cc

            result = PdfRenderResult.Succeeded
            Return myDoc

        End Using

    End Function


End Class



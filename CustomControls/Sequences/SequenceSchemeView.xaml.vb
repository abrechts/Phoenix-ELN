
Imports System.Drawing.Printing
Imports System.Globalization
Imports System.IO.Packaging
Imports System.Printing
Imports System.Threading
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Controls.Primitives
Imports System.Windows.Input
Imports System.Windows.Media
Imports System.Windows.Shapes
Imports System.Windows.Threading
Imports ElnCoreModel
Imports Spire.Pdf


Public Class SequenceSchemeView

    ' Layout constants (pixels)
    Private Const COL_GAP As Double = 120  ' horizontal gap between columns (routing space for connectors)
    Private Const ROW_GAP As Double = 50   ' vertical gap between rows in the same column
    Private Const PAD As Double = 30 '20       ' canvas padding on every side
    Private Const MERGE_COL_EXTRA_GAP As Double = 90  ' extra column gap before a merge target's column
    Private Const CONNECTOR_STRAIGHT_APPROACH As Double = 24.0 ' flat run just before docking onto a connector-step control
    Private Const CONNECTOR_STRAIGHT_DEPARTURE As Double = 24.0 ' flat run just after leaving a source panel that branches/merges
    Private Const BEZIER_HANDLE_LEN As Double = 60.0 ' fixed control-point offset near each Bezier endpoint (see BezierHandle)

    ' Maps each SequenceNode to the StackPanel that holds its step controls.
    Private _seqPanels As New Dictionary(Of SequenceGraph.SequenceNode, StackPanel)

    ' Maps a sequence whose first step's structure was omitted (because it
    ' duplicates its single upstream sequence's product) to the free-floating
    ' arrow control carrying that step's number/yield/seed marker. Positioned
    ' by DrawConnectors at the point where the connecting Bezier meets the
    ' sequence's panel.
    Private _connectorSteps As New Dictionary(Of SequenceGraph.SequenceNode, SequenceStructure)

    ' Stored so PrintScheme can re-render with a different structure color.
    Private _sequences As IReadOnlyList(Of SequenceGraph.SequenceNode)
    Private _queryExperiments As IQueryable(Of tblExperiments)

    ' Step selection state
    Private _currentlySelected As SequenceStructure = Nothing
    Private _seedStep As SequenceStructure = Nothing
    Private _stepClickHandlerAdded As Boolean = False

    ' ── Drag pan ──────────────────────────────────────────────────────────────

    Private _isMouseDown As Boolean
    Private _isDragging As Boolean
    Private _dragOrigin As Point
    Private _dragScrollOrigin As Point
    Private Const DRAG_THRESHOLD As Double = 5.0

    Private Sub SchemeScroll_PreviewMouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        If IsOnScrollBar(e.OriginalSource) Then Return
        _dragOrigin = e.GetPosition(SchemeScroll)
        _dragScrollOrigin = New Point(SchemeScroll.HorizontalOffset, SchemeScroll.VerticalOffset)
        _isMouseDown = True
        _isDragging = False
        SchemeScroll.CaptureMouse()
    End Sub

    Private Function IsOnScrollBar(source As Object) As Boolean
        Dim el = TryCast(source, DependencyObject)
        Do While el IsNot Nothing
            If TypeOf el Is ScrollBar Then Return True
            el = VisualTreeHelper.GetParent(el)
        Loop
        Return False
    End Function

    Private Sub SchemeScroll_PreviewMouseMove(sender As Object, e As MouseEventArgs)
        If Not _isMouseDown Then Return
        Dim pos = e.GetPosition(SchemeScroll)
        Dim dx = pos.X - _dragOrigin.X
        Dim dy = pos.Y - _dragOrigin.Y
        If Not _isDragging AndAlso (Math.Abs(dx) > DRAG_THRESHOLD OrElse Math.Abs(dy) > DRAG_THRESHOLD) Then
            _isDragging = True
            SchemeScroll.Cursor = Cursors.SizeAll
        End If
        If _isDragging Then
            SchemeScroll.ScrollToHorizontalOffset(_dragScrollOrigin.X - dx)
            SchemeScroll.ScrollToVerticalOffset(_dragScrollOrigin.Y - dy)
            e.Handled = True
        End If
    End Sub

    Private Sub SchemeScroll_PreviewMouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs)
        If _isDragging Then e.Handled = True
        _isMouseDown = False
        _isDragging = False
        SchemeScroll.Cursor = Nothing
        SchemeScroll.ReleaseMouseCapture()
    End Sub


    ' ── Zoom ──────────────────────────────────────────────────────────────────

    Private Const ZOOM_STEP As Double = 0.15
    Private Const ZOOM_MIN As Double = 0.2
    Private Const ZOOM_MAX As Double = 4.0
    Private Const WHEEL_ZOOM_MIN As Double = 0.25
    Private Const WHEEL_ZOOM_MAX As Double = 1.0

    Private _zoom As Double = 1.0
    Public Event ZoomChanged(zoom As Double)
    Public Event FitAllAvailableChanged(isAvailable As Boolean)

    Public Sub New()
        InitializeComponent()
    End Sub


    ' ── Public API ────────────────────────────────────────────────────────────

    ''' <summary>
    ''' Builds and renders the expanded combined scheme for all sequences.
    ''' Reuses the Level assignments already computed by SequenceGraph so the
    ''' column order matches the compressed graph above.
    ''' </summary>
    '''
    Public Sub BuildAndRender(sequences As IReadOnlyList(Of SequenceGraph.SequenceNode),
                               queryExperiments As IQueryable(Of tblExperiments))

        _sequences = sequences
        _queryExperiments = queryExperiments

        SchemeCanvas.Children.Clear()
        _seqPanels.Clear()
        _connectorSteps.Clear()
        _currentlySelected = Nothing
        _seedStep = Nothing

        If Not _stepClickHandlerAdded Then
            AddHandler SequenceStructure.StepArrowClicked, AddressOf OnStepArrowClicked
            _stepClickHandlerAdded = True
        End If

        If sequences Is Nothing OrElse sequences.Count = 0 Then Return

        SketchResults.ComponentStructureColor = New SolidColorBrush(
            CType(ColorConverter.ConvertFromString("#FFF9F96B"), Color))

        ' Phase 1 – build a StackPanel of SequenceStructure items for each sequence
        For Each seq In sequences
            If seq.Members.Count = 0 Then Continue For
            Dim panel = CreateSequencePanel(seq, queryExperiments, _connectorSteps)
            _seqPanels(seq) = panel
            Canvas.SetLeft(panel, 0)
            Canvas.SetTop(panel, 0)
            SchemeCanvas.Children.Add(panel)
        Next
        For Each connStep In _connectorSteps.Values
            Canvas.SetLeft(connStep, 0)
            Canvas.SetTop(connStep, 0)
            SchemeCanvas.Children.Add(connStep)
        Next

        ' Phase 2 – measure natural sizes (Canvas gives children infinite space)
        SchemeCanvas.UpdateLayout()

        ' Phase 3 – compute column-based positions and size the canvas
        PlaceSequencePanels(sequences)

        ' Phase 4 – draw connection arrows
        AddContentBorder(SchemeCanvas, _seqPanels)
        DrawConnectors(sequences, SchemeCanvas, _seqPanels, _connectorSteps)

        ' Phase 5 – fit the new content into the viewport once the ScrollViewer
        '           has processed the updated canvas size; then select the seed step
        Dispatcher.BeginInvoke(Sub()
                                   '   ApplyFitAllZoom()
                                   SelectStep(_seedStep)
                               End Sub, DispatcherPriority.Loaded)

    End Sub


    ''' <summary>
    ''' Deselects all step arrows in the combined scheme.
    ''' Called by the host dialog before selecting a new step.
    ''' </summary>
    '''
    Public Sub DeselectAllSteps()

        For Each panel In _seqPanels.Values
            For Each item In panel.Children.OfType(Of SequenceStructure)()
                item.IsSelected = False
            Next
        Next
        For Each item In _connectorSteps.Values
            item.IsSelected = False
        Next

    End Sub


    Private Sub SelectStep(stp As SequenceStructure)

        If _currentlySelected IsNot Nothing Then _currentlySelected.IsSelected = False
        _currentlySelected = stp
        If stp IsNot Nothing Then stp.IsSelected = True

    End Sub


    Private Sub OnStepArrowClicked(sender As Object)

        Dim stp = TryCast(sender, SequenceStructure)
        If stp Is Nothing Then Return

        ' Ignore clicks from SequenceStructures belonging to other views
        Dim allSteps = _seqPanels.Values.SelectMany(Function(p) p.Children.OfType(Of SequenceStructure)()).
            Concat(_connectorSteps.Values)
        If Not allSteps.Contains(stp) Then Return

        SelectStep(stp)

    End Sub


    ''' <summary>
    ''' Returns the "Sequence N" label for the sequence that contains the given
    ''' SequenceStructure, used to update the path label in the sidebar.
    ''' </summary>
    '''
    Public Function GetSequenceName(struct As SequenceStructure) As String

        For Each kvp In _seqPanels
            If kvp.Value.Children.OfType(Of SequenceStructure)().Contains(struct) Then
                Return $"Sequence {kvp.Key.Id + 1}"
            End If
        Next

        For Each kvp In _connectorSteps
            If ReferenceEquals(kvp.Value, struct) Then
                Return $"Sequence {kvp.Key.Id + 1}"
            End If
        Next

        Return ""

    End Function


    ' ── Print ─────────────────────────────────────────────────────────────────

    ''' <summary>
    ''' Prints the full scheme scaled to fit one landscape page.
    ''' Re-renders the structures with black ink on a separate off-tree canvas so
    ''' the screen view is unaffected and color is correct for paper output.
    ''' A header strip (app label + date) and a title block are drawn above the canvas.
    ''' </summary>
    '''
    Public Sub PrintScheme()

        If _sequences Is Nothing OrElse _seqPanels.Count = 0 Then Return

        DeselectAllSteps()
        _currentlySelected = Nothing

        Dim contentW = SchemeCanvas.Width
        Dim contentH = SchemeCanvas.Height
        If Double.IsNaN(contentW) OrElse contentW <= 0 OrElse
           Double.IsNaN(contentH) OrElse contentH <= 0 Then Return

        Dim pd As New PrintDialog()
        pd.PrintTicket.PageOrientation = PageOrientation.Landscape

        If Not pd.ShowDialog() Then Return

        Dim pageW = pd.PrintableAreaWidth
        Dim pageH = pd.PrintableAreaHeight
        ' Build a separate off-tree canvas with black structures.
        ' Connector coordinates are derived from _seqPanels (same positions/sizes)
        ' so no layout pass is needed for the new panels.
        Dim printCanvas = BuildPrintCanvas()

        Const PAGE_MARGIN As Double = 40.0
        Const HEADER_H As Double = 34.0
        Const TITLE_H As Double = 30.0
        Const HEADER_TITLE_GAP As Double = 14.0
        Const TITLE_CANVAS_GAP As Double = 4.0

        Dim availW = pageW - PAGE_MARGIN * 2
        Dim canvasTop = PAGE_MARGIN + HEADER_H + HEADER_TITLE_GAP + TITLE_H + TITLE_CANVAS_GAP
        Dim availH = pageH - canvasTop - PAGE_MARGIN
        Dim scale = Math.Min(availW / printCanvas.Width, availH / printCanvas.Height)

        Dim dpi = VisualTreeHelper.GetDpi(Me).PixelsPerDip
        Dim sansSerifNormal As New Typeface(New FontFamily("Segoe UI"),
                                            FontStyles.Normal, FontWeights.Normal, FontStretches.Normal)
        Dim sansSerifBold As New Typeface(New FontFamily("Segoe UI"),
                                          FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal)
        Dim grayBrush As Brush = New SolidColorBrush(Color.FromRgb(120, 120, 120))
        Dim titleBrush As Brush = New SolidColorBrush(Color.FromRgb(25, 55, 115))

        Dim printVisual As New DrawingVisual()
        Using dc = printVisual.RenderOpen()

            ' ── Header area ───────────────────────────────────────────────────

            Dim leftFt As New FormattedText("Phoenix ELN",
                System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                sansSerifNormal, 10, Brushes.Black, dpi)
            dc.DrawText(leftFt, New Point(PAGE_MARGIN, PAGE_MARGIN + (HEADER_H - leftFt.Height) / 2.0))

            Dim dateStr = DateTime.Today.ToString("MMMM d, yyyy")
            Dim rightFt As New FormattedText(dateStr,
                System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                sansSerifNormal, 10, Brushes.Black, dpi)
            dc.DrawText(rightFt, New Point(pageW - PAGE_MARGIN - rightFt.Width,
                                            PAGE_MARGIN + (HEADER_H - rightFt.Height) / 2.0))

            Dim sepY = PAGE_MARGIN + HEADER_H - 8
            dc.DrawLine(New Pen(Brushes.Gray, 0.7),
                        New Point(PAGE_MARGIN, sepY), New Point(pageW - PAGE_MARGIN, sepY))

            ' ── Title area ────────────────────────────────────────────────────

            Dim seqCount = _sequences.Count
            Dim stepCount = _sequences.Sum(Function(s) s.Members.Count)
            Dim titleText = $"Combined Synthesis Route  —  " &
                            $"{seqCount} Sequence{If(seqCount <> 1, "s", "")}, " &
                            $"{stepCount} Step{If(stepCount <> 1, "s", "")}"
            Dim titleFt As New FormattedText(titleText,
                System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                sansSerifBold, 12.5, titleBrush, dpi)
            Dim titleY = PAGE_MARGIN + HEADER_H + HEADER_TITLE_GAP + (TITLE_H - titleFt.Height) / 2.0
            dc.DrawText(titleFt, New Point(PAGE_MARGIN + (availW - titleFt.Width) / 2.0, titleY))

            ' ── Canvas area ───────────────────────────────────────────────────

            Dim brush As New VisualBrush(printCanvas) With {
                .Viewbox = New Rect(0, 0, printCanvas.Width, printCanvas.Height),
                .ViewboxUnits = BrushMappingMode.Absolute,
                .Stretch = Stretch.Fill
            }
            dc.DrawRectangle(brush, Nothing,
                             New Rect(PAGE_MARGIN, canvasTop,
                                      printCanvas.Width * scale, printCanvas.Height * scale))
        End Using

        pd.PrintVisual(printVisual, "Combined Sequence Scheme")

    End Sub


    ''' <summary>
    ''' Exports the full scheme to a PDF file using Spire.PDF.
    ''' Page size matches the system default paper format (A4, Letter, …) in landscape.
    ''' </summary>
    '''
    Public Sub ExportSchemeToPdf(pdfPath As String)

        If _sequences Is Nothing OrElse _seqPanels.Count = 0 Then Return

        DeselectAllSteps()
        _currentlySelected = Nothing

        Dim contentW = SchemeCanvas.Width
        Dim contentH = SchemeCanvas.Height
        If Double.IsNaN(contentW) OrElse contentW <= 0 OrElse
           Double.IsNaN(contentH) OrElse contentH <= 0 Then Return

        ' Query the default printer paper size; fall back to A4 if no printer installed.
        ' PaperSize dimensions are in 1/100 inch; convert to WPF DIUs (96 dpi).
        ' PaperSize is always portrait (Width ≤ Height) — swap for landscape.

        Dim printDoc As New PrintDocument()
        Dim ps As PaperSize = If(printDoc.PrinterSettings.IsValid, printDoc.DefaultPageSettings.PaperSize, New PaperSize("A4", 827, 1169))

        Dim shortSide As Double = ps.Width * 96.0 / 100.0
        Dim longSide As Double = ps.Height * 96.0 / 100.0
        Dim pageW As Double = longSide    ' landscape width
        Dim pageH As Double = shortSide   ' landscape height

        Dim printCanvas = BuildPrintCanvas()

        Const PAGE_MARGIN As Double = 40.0
        Const HEADER_H As Double = 34.0
        Const TITLE_H As Double = 30.0
        Const HEADER_TITLE_GAP As Double = 14.0
        Const TITLE_CANVAS_GAP As Double = 4.0

        Dim availW = pageW - PAGE_MARGIN * 2
        Dim canvasTop = PAGE_MARGIN + HEADER_H + HEADER_TITLE_GAP + TITLE_H + TITLE_CANVAS_GAP
        Dim availH = pageH - canvasTop - PAGE_MARGIN
        Dim scale = Math.Min(availW / printCanvas.Width, availH / printCanvas.Height)

        Dim dpi = VisualTreeHelper.GetDpi(Me).PixelsPerDip
        Dim sansSerifNormal As New Typeface(New FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal)
        Dim sansSerifBold As New Typeface(New FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal)
        Dim titleBrush As Brush = New SolidColorBrush(Color.FromRgb(25, 55, 115))

        Dim printVisual As New DrawingVisual()
        Using dc = printVisual.RenderOpen()

            Dim leftFt As New FormattedText("Phoenix ELN",
                CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                sansSerifNormal, 10, Brushes.Black, dpi)
            dc.DrawText(leftFt, New Point(PAGE_MARGIN, PAGE_MARGIN + (HEADER_H - leftFt.Height) / 2.0))

            Dim dateStr = DateTime.Today.ToString("MMMM d, yyyy")
            Dim rightFt As New FormattedText(dateStr,
                CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                sansSerifNormal, 10, Brushes.Black, dpi)
            dc.DrawText(rightFt, New Point(pageW - PAGE_MARGIN - rightFt.Width,
                                            PAGE_MARGIN + (HEADER_H - rightFt.Height) / 2.0))

            Dim sepY = PAGE_MARGIN + HEADER_H - 8
            dc.DrawLine(New Pen(Brushes.Gray, 0.7),
                        New Point(PAGE_MARGIN, sepY), New Point(pageW - PAGE_MARGIN, sepY))

            Dim seqCount = _sequences.Count
            Dim stepCount = _sequences.Sum(Function(s) s.Members.Count)
            Dim titleText = $"Structure Graph  —  " &
                            $"{seqCount} Sequence{If(seqCount <> 1, "s", "")}, " &
                            $"{stepCount} Step{If(stepCount <> 1, "s", "")}"
            Dim titleFt As New FormattedText(titleText,
                CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                sansSerifBold, 12.5, titleBrush, dpi)
            Dim titleY = PAGE_MARGIN + HEADER_H + HEADER_TITLE_GAP + (TITLE_H - titleFt.Height) / 2.0
            dc.DrawText(titleFt, New Point(PAGE_MARGIN + (availW - titleFt.Width) / 2.0, titleY))

            Dim brush As New VisualBrush(printCanvas) With {
                .Viewbox = New Rect(0, 0, printCanvas.Width, printCanvas.Height),
                .ViewboxUnits = BrushMappingMode.Absolute,
                .Stretch = Stretch.Fill
            }
            dc.DrawRectangle(brush, Nothing,
                             New Rect(PAGE_MARGIN, canvasTop,
                                      printCanvas.Width * scale, printCanvas.Height * scale))
        End Using

        Dim pt As New PrintTicket() With {
            .PageOrientation = PageOrientation.Landscape,
            .PageMediaSize = New PageMediaSize(shortSide, longSide)
        }

        Dim cc = Thread.CurrentThread.CurrentCulture
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture

        Dim saved = False
        Try
            Using xpsStream As New IO.MemoryStream()
                Dim pkg = Package.Open(xpsStream, IO.FileMode.Create, IO.FileAccess.ReadWrite)
                Dim xpsUri = New Uri("pack://TempScheme.xps")
                Dim xpsDoc = New Xps.Packaging.XpsDocument(pkg, CompressionOption.NotCompressed, xpsUri.AbsoluteUri)
                Dim writer = Xps.Packaging.XpsDocument.CreateXpsDocumentWriter(xpsDoc)
                writer.Write(printVisual, pt)
                xpsDoc.Close()
                pkg.Close()

                Dim pdfDoc As New PdfDocument()
                pdfDoc.LoadFromXPS(xpsStream)
                pdfDoc.SaveToFile(pdfPath)
            End Using
            saved = True

        Catch ex As Exception
            cbMsgBox.Display("Could not write PDF — the file may be open in a PDF viewer.", MsgBoxStyle.Exclamation, "PDF Export")
        Finally
            Thread.CurrentThread.CurrentCulture = cc
        End Try

        If saved Then
            Dim info As New ProcessStartInfo(pdfPath) With {.UseShellExecute = True}
            Process.Start(info)
        End If

    End Sub


    ''' <summary>
    ''' Creates an off-tree Canvas that mirrors the screen layout but renders
    ''' structure canvases with black ink (suitable for printing on white paper).
    ''' Panel positions are copied directly from _seqPanels so no additional
    ''' layout pass is needed; connector coordinates also reuse _seqPanels.
    ''' </summary>
    '''
    Private Function BuildPrintCanvas() As Canvas

        Dim printCanvas As New Canvas() With {
            .Width = SchemeCanvas.Width,
            .Height = SchemeCanvas.Height
        }

        ' Re-generate structure panels with black structures
        Dim printPanels As New Dictionary(Of SequenceGraph.SequenceNode, StackPanel)

        SketchResults.ComponentStructureColor = Brushes.Black

        Dim printConnectorSteps As New Dictionary(Of SequenceGraph.SequenceNode, SequenceStructure)

        For Each kvp In _seqPanels
            Dim seq = kvp.Key
            Dim printPanel = CreateSequencePanel(seq, _queryExperiments, printConnectorSteps)
            printPanels(seq) = printPanel
            ' Copy the exact same position computed during the screen layout pass
            Canvas.SetLeft(printPanel, Canvas.GetLeft(kvp.Value))
            Canvas.SetTop(printPanel, Canvas.GetTop(kvp.Value))
            printCanvas.Children.Add(printPanel)
        Next
        For Each connStep In printConnectorSteps.Values
            printCanvas.Children.Add(connStep)
        Next

        SketchResults.ComponentStructureColor = New SolidColorBrush(
            CType(ColorConverter.ConvertFromString("#FFF9F96B"), Color))

        ' Apply blue to arrows and step labels on the print panels; select the seed step
        Dim printBlue As New SolidColorBrush(Color.FromRgb(30, 100, 210))
        For Each panel In printPanels.Values
            For Each item In panel.Children.OfType(Of SequenceStructure)()
                item.ArrowColor = printBlue
                item.UpperLabelForeground = printBlue
                item.LowerLabelForeground = printBlue
                item.blkSeedSymbol.Foreground = printBlue
                If item.blkSeedSymbol.Visibility = Visibility.Visible Then
                    item.SelectionBorderBrush = New SolidColorBrush(Color.FromRgb(0, 124, 220))
                    item.IsSelected = True
                End If
            Next
        Next
        For Each item In printConnectorSteps.Values
            item.ArrowColor = printBlue
            item.UpperLabelForeground = printBlue
            item.LowerLabelForeground = printBlue
            item.blkSeedSymbol.Foreground = printBlue
            If item.blkSeedSymbol.Visibility = Visibility.Visible Then
                item.SelectionBorderBrush = New SolidColorBrush(Color.FromRgb(0, 124, 220))
                item.IsSelected = True
            End If
        Next

        ' Measure and arrange now so the free-floating connector-step controls
        ' (added directly to printCanvas rather than a panel) have a valid
        ' DesiredSize for DrawConnectors to position them by, and a valid
        ' Arrange pass so GetArrowAnchorY's TranslatePoint call resolves correctly.
        printCanvas.Measure(New Size(Double.PositiveInfinity, Double.PositiveInfinity))
        printCanvas.Arrange(New Rect(0, 0, printCanvas.Width, printCanvas.Height))

        ' Draw connectors using _seqPanels for coordinate lookup (same positions/sizes)
        ' and printCanvas as the render target.
        AddContentBorder(printCanvas, _seqPanels, Brushes.Gray)
        DrawConnectors(_sequences, printCanvas, _seqPanels, printConnectorSteps,
                       strokeOverride:=New SolidColorBrush(printBlue.Color),
                       arrowFillOverride:=New SolidColorBrush(printBlue.Color),
                       dotFillOverride:=New SolidColorBrush(printBlue.Color),
                       thicknessOverride:=1.0,
                       arrowSizeOverride:=6.5)

        ' One off-tree layout pass so PrintVisual can render the panels correctly
        printCanvas.Measure(New Size(Double.PositiveInfinity, Double.PositiveInfinity))
        printCanvas.Arrange(New Rect(0, 0, printCanvas.Width, printCanvas.Height))

        Return printCanvas

    End Function


    ' ── Zoom ──────────────────────────────────────────────────────────────────

    Private Sub SchemeScroll_PreviewMouseWheel(sender As Object, e As MouseWheelEventArgs)

        If Keyboard.Modifiers <> ModifierKeys.Control Then Return
        e.Handled = True
        Dim delta = If(e.Delta > 0, ZOOM_STEP, -ZOOM_STEP)
        ApplyZoom(Math.Min(Math.Max(_zoom + delta, WHEEL_ZOOM_MIN), WHEEL_ZOOM_MAX))

    End Sub


    Private Sub SchemeScroll_SizeChanged(sender As Object, e As SizeChangedEventArgs)

        UpdateFitAllAvailability()

    End Sub


    Public Sub ApplyZoom(level As Double)

        _zoom = level
        SchemeCanvas.LayoutTransform = New ScaleTransform(level, level)
        SyncZoomCombo()

    End Sub


    Public Sub ApplyFitAllZoom()

        Dim fitZoom = ComputeFitAllZoom()
        If fitZoom Is Nothing Then
            ApplyZoom(1.0)
            Return
        End If
        ApplyZoom(Math.Min(Math.Max(fitZoom.Value, ZOOM_MIN), 1.0))

    End Sub


    Private Function ComputeFitAllZoom() As Double?

        If SchemeCanvas.Width <= 0 OrElse SchemeCanvas.Height <= 0 OrElse
           SchemeScroll.ViewportWidth <= 0 OrElse SchemeScroll.ViewportHeight <= 0 Then
            Return Nothing
        End If
        Return Math.Min(SchemeScroll.ViewportWidth / SchemeCanvas.Width,
                        SchemeScroll.ViewportHeight / SchemeCanvas.Height)

    End Function


    Private Sub UpdateFitAllAvailability()

        Dim fitZoom = ComputeFitAllZoom()
        RaiseEvent FitAllAvailableChanged(fitZoom.HasValue AndAlso fitZoom.Value < 1.0)

    End Sub


    Private Sub SyncZoomCombo()

        RaiseEvent ZoomChanged(_zoom)

    End Sub


    ' ── Phase 1: build per-sequence step panels ────────────────────────────────

    ''' <summary>
    ''' Returns whether the sequence's last member has a resolvable product
    ''' structure — i.e. whether its panel would end with a trailing product cap.
    ''' </summary>
    '''
    Private Function SequenceEndsWithProduct(seq As SequenceGraph.SequenceNode) As Boolean

        Dim lastMember = seq.Members.LastOrDefault()
        If lastMember Is Nothing Then Return False

        Dim expItem = TryCast(lastMember.ConnectItem.AttachedItem, tblExperiments)
        If expItem Is Nothing OrElse expItem.RxnSketch Is Nothing Then Return False

        Dim skInfo = DrawingEditor.GetSketchInfo(expItem.RxnSketch)
        Return skInfo.Products.Count > 0

    End Function


    ''' <summary>
    ''' A sequence's first step can be collapsed onto its incoming connector(s)
    ''' — omitting its own reactant structure, which duplicates whatever
    ''' upstream sequence(s) produce it — whenever it has at least one incoming
    ''' sequence. Every upstream sequence keeps its own trailing product cap
    ''' rendered as-is (see CreateSequencePanel), so with ≥2 incoming sequences
    ''' (a convergent merge onto the same intermediate), those matching product
    ''' caps are the visual anchors the shared junction dot connects to (see
    ''' DrawMergeGroup).
    ''' </summary>
    '''
    Private Function ShouldCollapseFirstStep(seq As SequenceGraph.SequenceNode) As Boolean

        If seq.Incoming.Count = 0 Then Return False
        If seq.Incoming.Count = 1 AndAlso Not SequenceEndsWithProduct(seq.Incoming(0)) Then Return False
        ' A single-member sequence with no product of its own would collapse
        ' to an empty panel — keep it rendered in full instead.
        If seq.Members.Count = 1 AndAlso Not SequenceEndsWithProduct(seq) Then Return False

        Return True

    End Function


    Private Function CreateSequencePanel(seq As SequenceGraph.SequenceNode,
                                          queryExperiments As IQueryable(Of tblExperiments),
                                          connectorSteps As Dictionary(Of SequenceGraph.SequenceNode, SequenceStructure)) As StackPanel

        Dim panel As New StackPanel With {.Orientation = Orientation.Horizontal}
        Dim stepNr As Integer = 1
        Dim collapseFirst = ShouldCollapseFirstStep(seq)

        For Each seqStep In seq.Members

            Dim expItem = TryCast(seqStep.ConnectItem.AttachedItem, tblExperiments)
            If expItem Is Nothing OrElse expItem.RxnSketch Is Nothing Then Continue For

            Dim skInfo = DrawingEditor.GetSketchInfo(expItem.RxnSketch)
            If skInfo.Reactants.Count = 0 Then Continue For

            Dim isCollapsedFirst = collapseFirst AndAlso seqStep Is seq.Members.First

            ' ── Reactant step (or, for a collapsed first step, a free-floating
            '    arrow-only control positioned later by DrawConnectors) ───────

            Dim stepStruct As New SequenceStructure
            stepStruct.SetStepExperiments(expItem, queryExperiments)
            If isCollapsedFirst Then
                stepStruct.HideReactantStructure()
            Else
                stepStruct.StructureCanvas = skInfo.Reactants.First.StructureCanvas
            End If
            stepStruct.StepNumberStr = "Step " & stepNr.ToString

            If seqStep.ConnectItem.IsSeed Then
                stepStruct.blkSeedSymbol.Visibility = Visibility.Visible
                If _seedStep Is Nothing Then _seedStep = stepStruct
            End If

            Dim maxYield = stepStruct.StepExperiments.Max(Function(x) x.Yield)
            If maxYield IsNot Nothing Then
                Dim prefix = If(stepStruct.StepExperiments.Count > 1, "≤ ", "")
                stepStruct.blkExpCount.Text = prefix & Format(maxYield, "0") & "%"
            Else
                stepStruct.blkExpCount.Text = "no yield"
            End If

            stepNr += 1

            If isCollapsedFirst Then
                connectorSteps(seq) = stepStruct
            Else
                panel.Children.Add(stepStruct)
            End If

            ' ── Product structure after the last step ─────────────────────────
            ' Always rendered when present, even when this sequence feeds a
            ' downstream merge — keeping the shared intermediate visually
            ' consistent across all of the merge's incoming sequences.

            If seqStep Is seq.Members.Last AndAlso skInfo.Products.Count > 0 Then
                Dim prodStruct As New SequenceStructure With {
                    .StructureCanvas = skInfo.Products.First.StructureCanvas
                }
                prodStruct.HideRightArrow()
                panel.Children.Add(prodStruct)
            End If

        Next

        Return panel

    End Function


    ' ── Phase 3: column-based layout ──────────────────────────────────────────

    Private Sub PlaceSequencePanels(sequences As IReadOnlyList(Of SequenceGraph.SequenceNode))

        If sequences.Count = 0 Then Return

        Dim maxLevel = sequences.Max(Function(s) s.Level)

        ' Group sequences by level, keeping only those that have a rendered panel
        Dim byLevel As New Dictionary(Of Integer, List(Of SequenceGraph.SequenceNode))
        For lvl = 0 To maxLevel
            Dim thisLevel = lvl   'place in a separate variable for lambda function within loop
            byLevel(thisLevel) = sequences.Where(Function(s) s.Level = thisLevel AndAlso _seqPanels.ContainsKey(s)).ToList()
        Next

        ' Column widths = max DesiredSize.Width of all panels in that column
        Dim colWidths(maxLevel) As Double
        For lvl = 0 To maxLevel
            For Each seq In byLevel(lvl)
                colWidths(lvl) = Math.Max(colWidths(lvl), _seqPanels(seq).DesiredSize.Width)
            Next
        Next

        ' Cumulative X start for each column. Columns that receive a merge
        ' (≥2 incoming sequences converging on one collapsed connector step,
        ' see DrawMergeGroup) get extra breathing room so the fan-out curves
        ' have enough horizontal runway before docking onto the connector.
        ' Only columns that will actually show a shared junction dot (≥2
        ' targets converging on the same ≥2 sources, see DrawMergeGroup) need
        ' the extra gap — a single-target merge draws no dot and doesn't need it.
        Dim dotMergeGroups = FindMergeGroups(_seqPanels, _connectorSteps).Where(Function(g) g.Targets.Count >= 2).ToList()

        Dim colX(maxLevel) As Double
        colX(0) = PAD
        For lvl = 1 To maxLevel
            Dim thisLevelSeqs = byLevel(lvl)
            Dim hasSharedDot = dotMergeGroups.Any(Function(g) g.Targets.Any(Function(t) thisLevelSeqs.Contains(t)))
            Dim gap = COL_GAP + If(hasSharedDot, MERGE_COL_EXTRA_GAP, 0.0)
            colX(lvl) = colX(lvl - 1) + colWidths(lvl - 1) + gap
        Next

        ' Tallest column height (for vertical centering of shorter columns)
        Dim maxColH = 0.0
        For lvl = 0 To maxLevel
            Dim seqs = byLevel(lvl)
            If seqs.Count = 0 Then Continue For
            Dim colH = seqs.Sum(Function(s) _seqPanels(s).DesiredSize.Height) +
                       (seqs.Count - 1) * ROW_GAP
            If colH > maxColH Then maxColH = colH
        Next

        ' Assign Canvas.Left / Canvas.Top to every panel
        For lvl = 0 To maxLevel
            Dim seqs = byLevel(lvl)
            Dim colH = If(seqs.Count = 0, 0.0,
                seqs.Sum(Function(s) _seqPanels(s).DesiredSize.Height) +
                (seqs.Count - 1) * ROW_GAP)
            Dim y = PAD + (maxColH - colH) / 2.0
            For Each seq In seqs
                Dim panel = _seqPanels(seq)
                Canvas.SetLeft(panel, colX(lvl))
                Canvas.SetTop(panel, y)
                y += panel.DesiredSize.Height + ROW_GAP
            Next
        Next

        ' Size the canvas to fit all content
        SchemeCanvas.Width = If(maxLevel >= 0, colX(maxLevel) + colWidths(maxLevel) + PAD, PAD * 2)
        SchemeCanvas.Height = PAD * 2 + maxColH

    End Sub


    ' ── Junction bundle ───────────────────────────────────────────────────────

    Private Class JunctionBundle
        Public Property Sources As New List(Of SequenceGraph.SequenceNode)
        Public Property Targets As New List(Of SequenceGraph.SequenceNode)
        Public Property Jx As Double
        Public Property Jy As Double
    End Class


    ' ── Merge group (≥2 incoming sequences converging on one collapsed step) ───

    Private Class MergeGroup
        Public Property Targets As New List(Of SequenceGraph.SequenceNode)
        Public Property Sources As New List(Of SequenceGraph.SequenceNode)
    End Class


    ' ── Content border ────────────────────────────────────────────────────────

    Private Sub AddContentBorder(targetCanvas As Canvas,
                                  panels As Dictionary(Of SequenceGraph.SequenceNode, StackPanel),
                                  Optional strokeBrush As Brush = Nothing)

        If panels.Count = 0 Then Return

        Dim minX = Double.MaxValue
        Dim minY = Double.MaxValue
        Dim maxX = Double.MinValue
        Dim maxY = Double.MinValue

        For Each panel In panels.Values
            Dim left = Canvas.GetLeft(panel)
            Dim top = Canvas.GetTop(panel)
            If left < minX Then minX = left
            If top < minY Then minY = top
            If left + panel.DesiredSize.Width > maxX Then maxX = left + panel.DesiredSize.Width
            If top + panel.DesiredSize.Height > maxY Then maxY = top + panel.DesiredSize.Height
        Next

        Const MARGIN As Double = 12.0
        Dim rect As New Rectangle With {
            .Width = maxX - minX + MARGIN * 2,
            .Height = maxY - minY + MARGIN * 2,
            .RadiusX = 10,
            .RadiusY = 10,
            .Stroke = If(strokeBrush, New SolidColorBrush(Color.FromRgb(90, 90, 90))),
            .StrokeThickness = 1.5,
            .Fill = Brushes.Transparent
        }
        Canvas.SetLeft(rect, minX - MARGIN)
        Canvas.SetTop(rect, minY - MARGIN)
        targetCanvas.Children.Add(rect)

    End Sub


    ' ── Phase 4: draw connection arrows ───────────────────────────────────────
    '
    '  All drawing helpers accept targetCanvas + panels as parameters so the
    '  same logic serves both the live SchemeCanvas and the off-tree print canvas.

    Private Sub DrawConnectors(sequences As IReadOnlyList(Of SequenceGraph.SequenceNode),
                                targetCanvas As Canvas,
                                panels As Dictionary(Of SequenceGraph.SequenceNode, StackPanel),
                                connectorSteps As Dictionary(Of SequenceGraph.SequenceNode, SequenceStructure),
                                Optional strokeOverride As Brush = Nothing,
                                Optional arrowFillOverride As Brush = Nothing,
                                Optional dotFillOverride As Brush = Nothing,
                                Optional thicknessOverride As Double = 2.0,
                                Optional arrowSizeOverride As Double = 9.0)

        Dim stroke As Brush = If(strokeOverride, New SolidColorBrush(Color.FromArgb(170, 120, 150, 205)))
        Dim arrowFill As Brush = If(arrowFillOverride, New SolidColorBrush(Color.FromArgb(200, 135, 160, 215)))
        Dim dotFill As Brush = If(dotFillOverride, New SolidColorBrush(Color.FromRgb(105, 140, 200)))
        Dim thickness = thicknessOverride
        Dim arrowSize = arrowSizeOverride

        Dim bundles = FindJunctionBundles(panels)

        Dim bundled As New HashSet(Of String)
        For Each b In bundles
            For Each src In b.Sources
                For Each tgt In b.Targets
                    bundled.Add($"{src.Id}_{tgt.Id}")
                Next
            Next
        Next

        For Each b In bundles
            DrawJunctionBundle(b, stroke, arrowFill, dotFill, thickness, arrowSize, targetCanvas, panels)
        Next

        Dim mergeGroups = FindMergeGroups(panels, connectorSteps)
        For Each g In mergeGroups
            For Each src In g.Sources
                For Each tgt In g.Targets
                    bundled.Add($"{src.Id}_{tgt.Id}")
                Next
            Next
            DrawMergeGroup(g, stroke, arrowFill, dotFill, thickness, arrowSize, targetCanvas, panels, connectorSteps)
        Next

        For Each seq In sequences
            If Not panels.ContainsKey(seq) Then Continue For

            Dim edges = seq.Outgoing.
                Where(Function(t) Not bundled.Contains($"{seq.Id}_{t.Id}") AndAlso panels.ContainsKey(t)).ToList()
            If edges.Count = 0 Then Continue For

            Dim srcPanel = panels(seq)
            Dim x1 = Canvas.GetLeft(srcPanel) + srcPanel.DesiredSize.Width
            Dim y1 = Canvas.GetTop(srcPanel) + srcPanel.DesiredSize.Height / 2.0

            ' When this source branches to ≥2 targets, draw the flat launch
            ' stub once, shared by all of them, instead of once per edge —
            ' every edge starts at the exact same (x1, y1), so drawing the
            ' stub per edge would stack identical, overlapping strokes on top
            ' of each other (visibly darker, with a jagged seam where their
            ' anti-aliased edges don't quite coincide).
            Dim minRun = edges.Min(Function(t)
                                       Dim effectiveX2 = Canvas.GetLeft(panels(t))
                                       Dim cs As SequenceStructure = Nothing
                                       If connectorSteps.TryGetValue(t, cs) Then
                                           effectiveX2 = Math.Max(effectiveX2 - cs.DesiredSize.Width, x1 + 10)
                                       End If
                                       Return effectiveX2 - x1
                                   End Function)
            Dim departure = If(edges.Count > 1, Math.Max(0.0, Math.Min(CONNECTOR_STRAIGHT_DEPARTURE, minRun * 0.4)), 0.0)
            Dim curveStartX = x1 + departure

            If departure > 0 Then
                Dim stubFig As New PathFigure With {.StartPoint = New Point(x1, y1)}
                stubFig.Segments.Add(New LineSegment(New Point(curveStartX, y1), True))
                Dim stubGeo As New PathGeometry()
                stubGeo.Figures.Add(stubFig)
                targetCanvas.Children.Add(New Path With {.Data = stubGeo, .Stroke = stroke, .StrokeThickness = thickness})
            End If

            For Each target In edges
                Dim tgtPanel = panels(target)
                Dim x2 = Canvas.GetLeft(tgtPanel)
                Dim y2 = Canvas.GetTop(tgtPanel) + tgtPanel.DesiredSize.Height / 2.0

                If x2 <= x1 + 1 Then Continue For

                ' If the target's first step was collapsed onto this connector
                ' (its reactant structure omitted because it duplicates this
                ' source's product), land the Bezier on the free-floating arrow
                ' control instead of drawing a plain arrowhead into the panel.
                Dim connStep As SequenceStructure = Nothing
                If connectorSteps.TryGetValue(target, connStep) Then
                    Dim cw = connStep.DesiredSize.Width
                    Dim cx = Math.Max(x2 - cw, x1 + 10)
                    Dim cy = y2 - connStep.GetArrowAnchorY()
                    Canvas.SetLeft(connStep, cx)
                    Canvas.SetTop(connStep, cy)
                    DrawBezierArrow(curveStartX, y1, cx, y2, stroke, targetCanvas, showArrowhead:=False, straightApproach:=CONNECTOR_STRAIGHT_APPROACH,
                                    straightDeparture:=If(departure = 0.0, CONNECTOR_STRAIGHT_DEPARTURE, 0.0), arrowFill:=arrowFill, thickness:=thickness, arrowSize:=arrowSize)
                Else
                    DrawBezierArrow(curveStartX, y1, x2, y2, stroke, targetCanvas,
                                    straightDeparture:=If(departure = 0.0, CONNECTOR_STRAIGHT_DEPARTURE, 0.0), arrowFill:=arrowFill, thickness:=thickness, arrowSize:=arrowSize)
                End If
            Next
        Next

    End Sub


    ''' <summary>
    ''' Bundles sources that fan out to an identical set of ≥2 plain targets
    ''' into a shared junction dot. Targets fed by ≥2 incoming sequences (a
    ''' convergent merge onto one collapsed connector control) are excluded
    ''' here — those are exclusively owned by <see cref="FindMergeGroups"/>,
    ''' otherwise the same source/target pair would get two overlapping dots.
    ''' </summary>
    '''
    Private Function FindJunctionBundles(panels As Dictionary(Of SequenceGraph.SequenceNode, StackPanel)) As List(Of JunctionBundle)

        Dim result As New List(Of JunctionBundle)
        Dim groups As New Dictionary(Of String, List(Of SequenceGraph.SequenceNode))

        For Each seq In panels.Keys

            If seq.Outgoing.Count < 2 Then Continue For

            Dim sig = String.Join("|", seq.Outgoing.
                Where(Function(t) panels.ContainsKey(t) AndAlso t.Incoming.Count < 2).
                Select(Function(t) t.Id.ToString()).
                OrderBy(Function(s) s))
            If sig = "" Then Continue For

            Dim value As List(Of SequenceGraph.SequenceNode) = Nothing
            If Not groups.TryGetValue(sig, value) Then value = New List(Of SequenceGraph.SequenceNode)
            groups(sig) = value
            value.Add(seq)

        Next

        For Each kvp In groups
            If kvp.Value.Count < 2 Then Continue For

            Dim sources = kvp.Value
            Dim targets = sources(0).Outgoing.
                Where(Function(t) panels.ContainsKey(t) AndAlso t.Incoming.Count < 2).
                OrderBy(Function(t) Canvas.GetTop(panels(t))).ToList()

            Dim srcRight = sources.Max(Function(s) Canvas.GetLeft(panels(s)) + panels(s).DesiredSize.Width)
            Dim tgtLeft = targets.Min(Function(t) Canvas.GetLeft(panels(t)))
            Dim jx = (srcRight + tgtLeft) / 2.0

            Dim srcMidY = sources.Average(Function(s) Canvas.GetTop(panels(s)) + panels(s).DesiredSize.Height / 2.0)
            Dim tgtMidY = targets.Average(Function(t) Canvas.GetTop(panels(t)) + panels(t).DesiredSize.Height / 2.0)
            Dim jy = (srcMidY + tgtMidY) / 2.0

            result.Add(New JunctionBundle With {.Sources = sources, .Targets = targets, .Jx = jx, .Jy = jy})
        Next

        Return result

    End Function


    Private Sub DrawJunctionBundle(b As JunctionBundle, stroke As Brush, arrowFill As Brush, dotFill As Brush,
                                    thickness As Double, arrowSize As Double,
                                    targetCanvas As Canvas,
                                    panels As Dictionary(Of SequenceGraph.SequenceNode, StackPanel))

        For Each src In b.Sources
            Dim p = panels(src)
            Dim x1 = Canvas.GetLeft(p) + p.DesiredSize.Width
            Dim y1 = Canvas.GetTop(p) + p.DesiredSize.Height / 2.0
            Dim dx = BezierHandle(b.Jx - x1)
            Dim fig As New PathFigure With {.StartPoint = New Point(x1, y1)}
            fig.Segments.Add(New BezierSegment(New Point(x1 + dx, y1), New Point(b.Jx - dx, b.Jy), New Point(b.Jx, b.Jy), True))
            Dim geo As New PathGeometry()
            geo.Figures.Add(fig)
            targetCanvas.Children.Add(New Path With {.Data = geo, .Stroke = stroke, .StrokeThickness = thickness})
        Next

        Dim arrowDepth = arrowSize * Math.Cos(Math.PI / 6.0)
        For Each tgt In b.Targets
            Dim p = panels(tgt)
            Dim x2 = Canvas.GetLeft(p)
            Dim y2 = Canvas.GetTop(p) + p.DesiredSize.Height / 2.0
            Dim lineX = x2 - arrowDepth
            Dim dx = BezierHandle(lineX - b.Jx)
            Dim fig As New PathFigure With {.StartPoint = New Point(b.Jx, b.Jy)}
            fig.Segments.Add(New BezierSegment(New Point(b.Jx + dx, b.Jy), New Point(lineX - dx, y2), New Point(lineX, y2), True))
            Dim geo As New PathGeometry()
            geo.Figures.Add(fig)
            targetCanvas.Children.Add(New Path With {.Data = geo, .Stroke = stroke, .StrokeThickness = thickness})
            DrawArrowHead(x2, y2, arrowSize, arrowFill, targetCanvas)
        Next

        Const DOT_DIAM As Double = 12.0
        Dim jDot As New Ellipse With {
            .Width = DOT_DIAM,
            .Height = DOT_DIAM,
            .Fill = dotFill,
            .Stroke = New SolidColorBrush(Colors.WhiteSmoke),
            .StrokeThickness = 1.5,
            .ToolTip = "Common Intermediate"
        }
        Canvas.SetLeft(jDot, b.Jx - DOT_DIAM / 2.0)
        Canvas.SetTop(jDot, b.Jy - DOT_DIAM / 2.0)
        targetCanvas.Children.Add(jDot)

    End Sub


    ''' <summary>
    ''' Finds targets fed by ≥2 rendered incoming sequences that all produce the
    ''' same shared intermediate (a convergent merge). Each such target has had
    ''' its first step collapsed onto a connector control (see
    ''' <see cref="ShouldCollapseFirstStep"/>), while every source still renders
    ''' its own trailing product cap for that shared intermediate, so the merge
    ''' is drawn as sources (each ending on its product cap) → shared dot →
    ''' connector control(s). Targets sharing the exact same set of ≥2 sources
    ''' (e.g. three downstream sequences all starting from the same pair of
    ''' upstream intermediates) are combined into a single MergeGroup so they
    ''' share one junction dot instead of drawing one dot per target.
    ''' </summary>
    '''
    Private Function FindMergeGroups(panels As Dictionary(Of SequenceGraph.SequenceNode, StackPanel),
                                      connectorSteps As Dictionary(Of SequenceGraph.SequenceNode, SequenceStructure)) As List(Of MergeGroup)

        Dim groups As New Dictionary(Of String, MergeGroup)

        For Each target In connectorSteps.Keys
            If target.Incoming.Count < 2 Then Continue For
            If Not panels.ContainsKey(target) Then Continue For

            Dim sources = target.Incoming.Where(Function(s) panels.ContainsKey(s)).ToList()
            If sources.Count < 2 Then Continue For

            Dim sig = String.Join("|", sources.Select(Function(s) s.Id.ToString()).OrderBy(Function(s) s))

            Dim g As MergeGroup = Nothing
            If Not groups.TryGetValue(sig, g) Then
                g = New MergeGroup With {.Sources = sources}
                groups(sig) = g
            End If
            g.Targets.Add(target)
        Next

        Return groups.Values.ToList()

    End Function


    ''' <summary>
    ''' Draws a merge group. The bundling dot is only meaningful when both
    ''' sides fan out — ≥2 incoming sources AND ≥2 outgoing targets sharing
    ''' the same intermediate. With a single target, each source connects
    ''' straight to that target's connector control instead.
    ''' </summary>
    '''
    Private Sub DrawMergeGroup(g As MergeGroup, stroke As Brush, arrowFill As Brush, dotFill As Brush,
                                thickness As Double, arrowSize As Double,
                                targetCanvas As Canvas,
                                panels As Dictionary(Of SequenceGraph.SequenceNode, StackPanel),
                                connectorSteps As Dictionary(Of SequenceGraph.SequenceNode, SequenceStructure))

        Dim srcRight = g.Sources.Max(Function(s) Canvas.GetLeft(panels(s)) + panels(s).DesiredSize.Width)

        ' Single shared X for every target's connector control, so all of them
        ' line up at the same distance from the source(s).
        Dim cx = Math.Max(g.Targets.Min(Function(t) Canvas.GetLeft(panels(t)) - connectorSteps(t).DesiredSize.Width),
                          srcRight + 10)

        For Each tgt In g.Targets
            Dim tgtPanel = panels(tgt)
            Dim connStep = connectorSteps(tgt)
            Dim cy = Canvas.GetTop(tgtPanel) + tgtPanel.DesiredSize.Height / 2.0 - connStep.GetArrowAnchorY()
            Canvas.SetLeft(connStep, cx)
            Canvas.SetTop(connStep, cy)
        Next

        If g.Targets.Count = 1 Then
            Dim connStep = connectorSteps(g.Targets(0))
            Dim cy2 = Canvas.GetTop(connStep) + connStep.GetArrowAnchorY()

            If g.Sources.Count = 1 Then
                Dim p = panels(g.Sources(0))
                Dim x1 = Canvas.GetLeft(p) + p.DesiredSize.Width
                Dim y1 = Canvas.GetTop(p) + p.DesiredSize.Height / 2.0
                DrawBezierArrow(x1, y1, cx, cy2, stroke, targetCanvas, showArrowhead:=False, straightApproach:=CONNECTOR_STRAIGHT_APPROACH, arrowFill:=arrowFill, thickness:=thickness, arrowSize:=arrowSize)
                Return
            End If

            ' ≥2 sources converge on the same connector — draw the shared
            ' final approach once rather than once per source. Every curve
            ' ends at the exact same (cx, cy2), so a per-source approach
            ' segment would stack identical, overlapping strokes on top of
            ' each other (visibly darker, with a jagged seam where their
            ' anti-aliased edges don't quite coincide).
            Dim minRun = g.Sources.Min(Function(s) cx - (Canvas.GetLeft(panels(s)) + panels(s).DesiredSize.Width))
            Dim approach = Math.Max(0.0, Math.Min(CONNECTOR_STRAIGHT_APPROACH, minRun * 0.4))
            Dim curveEndX = cx - approach

            For Each src In g.Sources
                Dim p = panels(src)
                Dim x1 = Canvas.GetLeft(p) + p.DesiredSize.Width
                Dim y1 = Canvas.GetTop(p) + p.DesiredSize.Height / 2.0
                Dim dx = BezierHandle(curveEndX - x1)
                Dim fig As New PathFigure With {.StartPoint = New Point(x1, y1)}
                fig.Segments.Add(New BezierSegment(New Point(x1 + dx, y1), New Point(curveEndX - dx, cy2), New Point(curveEndX, cy2), True))
                Dim geo As New PathGeometry()
                geo.Figures.Add(fig)
                targetCanvas.Children.Add(New Path With {.Data = geo, .Stroke = stroke, .StrokeThickness = thickness})
            Next

            If approach > 0 Then
                Dim stubFig As New PathFigure With {.StartPoint = New Point(curveEndX, cy2)}
                stubFig.Segments.Add(New LineSegment(New Point(cx, cy2), True))
                Dim stubGeo As New PathGeometry()
                stubGeo.Figures.Add(stubFig)
                targetCanvas.Children.Add(New Path With {.Data = stubGeo, .Stroke = stroke, .StrokeThickness = thickness})
            End If

            Return
        End If

        Dim srcMidY = g.Sources.Average(Function(s) Canvas.GetTop(panels(s)) + panels(s).DesiredSize.Height / 2.0)
        Dim tgtMidY = g.Targets.Average(Function(t) Canvas.GetTop(panels(t)) + panels(t).DesiredSize.Height / 2.0)
        Dim jx = (srcRight + cx) / 2.0
        Dim jy = (srcMidY + tgtMidY) / 2.0

        For Each src In g.Sources
            Dim p = panels(src)
            Dim x1 = Canvas.GetLeft(p) + p.DesiredSize.Width
            Dim y1 = Canvas.GetTop(p) + p.DesiredSize.Height / 2.0
            Dim dx = BezierHandle(jx - x1)
            Dim fig As New PathFigure With {.StartPoint = New Point(x1, y1)}
            fig.Segments.Add(New BezierSegment(New Point(x1 + dx, y1), New Point(jx - dx, jy), New Point(jx, jy), True))
            Dim geo As New PathGeometry()
            geo.Figures.Add(fig)
            targetCanvas.Children.Add(New Path With {.Data = geo, .Stroke = stroke, .StrokeThickness = thickness})
        Next

        Const DOT_DIAM As Double = 12.0
        Dim jDot As New Ellipse With {
            .Width = DOT_DIAM,
            .Height = DOT_DIAM,
            .Fill = dotFill,
            .Stroke = New SolidColorBrush(Colors.WhiteSmoke),
            .StrokeThickness = 1.5,
            .ToolTip = "Common Intermediate"
        }
        Canvas.SetLeft(jDot, jx - DOT_DIAM / 2.0)
        Canvas.SetTop(jDot, jy - DOT_DIAM / 2.0)
        targetCanvas.Children.Add(jDot)

        For Each tgt In g.Targets
            Dim connStep = connectorSteps(tgt)
            Dim cy2 = Canvas.GetTop(connStep) + connStep.GetArrowAnchorY()
            DrawBezierArrow(jx, jy, cx, cy2, stroke, targetCanvas, showArrowhead:=False, straightApproach:=CONNECTOR_STRAIGHT_APPROACH, arrowFill:=arrowFill, thickness:=thickness, arrowSize:=arrowSize)
        Next

    End Sub


    ''' <summary>
    ''' Fixed control-point offset for a Bezier segment spanning the given
    ''' distance, capped so the two handles never cross on very short spans.
    ''' Using a fixed length (rather than a fraction of the total distance)
    ''' keeps the curve's bend near each endpoint visually consistent no
    ''' matter how far apart the two points are — a proportional offset makes
    ''' long curves flatten out gradually over a much longer visible stretch
    ''' than short ones.
    ''' </summary>
    '''
    Private Function BezierHandle(distance As Double) As Double

        Return Math.Min(BEZIER_HANDLE_LEN, Math.Abs(distance) * 0.5)

    End Function


    ''' <summary>
    ''' Draws a Bezier connector, ending in an arrowhead unless the far end
    ''' docks directly onto a connector-step control — that control already
    ''' shows its own step arrow, so an extra arrowhead there would just
    ''' overlap it (see <see cref="DrawArrowHead"/> callers). When
    ''' <paramref name="straightApproach"/> is > 0, the curve is shortened by
    ''' that much (capped to a fraction of the total run, so it never eats
    ''' more than the available horizontal space) and finished with a flat
    ''' horizontal run into (x2, y2), so the line meets the target's own
    ''' arrow head-on instead of still visibly curving right up to it.
    ''' </summary>
    '''
    Private Sub DrawBezierArrow(x1 As Double, y1 As Double, x2 As Double, y2 As Double, stroke As Brush, targetCanvas As Canvas,
                                 Optional showArrowhead As Boolean = True,
                                 Optional straightApproach As Double = 0.0,
                                 Optional straightDeparture As Double = 0.0,
                                 Optional arrowFill As Brush = Nothing,
                                 Optional thickness As Double = 2.0,
                                 Optional arrowSize As Double = 9.0)

        Dim totalRun = x2 - x1
        Dim approach = Math.Max(0.0, Math.Min(straightApproach, totalRun * 0.4))
        Dim departure = Math.Max(0.0, Math.Min(straightDeparture, totalRun * 0.4))
        Dim curveStartX = x1 + departure
        Dim curveEndX = x2 - approach
        Dim dx = BezierHandle(curveEndX - curveStartX)
        Dim fig As New PathFigure With {.StartPoint = New Point(x1, y1)}
        If departure > 0 Then
            fig.Segments.Add(New LineSegment(New Point(curveStartX, y1), True))
        End If
        fig.Segments.Add(New BezierSegment(
            New Point(curveStartX + dx, y1),
            New Point(curveEndX - dx, y2),
            New Point(curveEndX, y2), True))
        If approach > 0 Then
            fig.Segments.Add(New LineSegment(New Point(x2, y2), True))
        End If
        Dim geo As New PathGeometry()
        geo.Figures.Add(fig)

        targetCanvas.Children.Add(New Path With {
            .Data = geo,
            .Stroke = stroke,
            .StrokeThickness = thickness
        })

        If showArrowhead Then DrawArrowHead(x2, y2, arrowSize, arrowFill, targetCanvas)

    End Sub


    Private Sub DrawArrowHead(tipX As Double, tipY As Double, sz As Double, fill As Brush, targetCanvas As Canvas)

        Dim half = Math.PI / 6.0
        Dim p1 = New Point(tipX + sz * Math.Cos(Math.PI + half), tipY + sz * Math.Sin(Math.PI + half))
        Dim p2 = New Point(tipX + sz * Math.Cos(Math.PI - half), tipY + sz * Math.Sin(Math.PI - half))

        Dim fig As New PathFigure With {.StartPoint = New Point(tipX, tipY), .IsClosed = True}
        fig.Segments.Add(New LineSegment(p1, True))
        fig.Segments.Add(New LineSegment(p2, True))
        Dim geo As New PathGeometry()
        geo.Figures.Add(fig)

        targetCanvas.Children.Add(New Path With {
            .Data = geo,
            .Fill = If(fill, New SolidColorBrush(Color.FromArgb(175, 100, 150, 230)))
        })

    End Sub

End Class

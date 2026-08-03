Imports System.Globalization
Imports System.Windows
Imports System.Windows.Media
Imports ElnBase

''' <summary>
''' Renders a full-text search snippet (as produced by FullTextSearch, with matched terms wrapped in
''' FullTextSearch.HighlightStartMarker/HighlightEndMarker) with matched terms shown on a yellow highlight.
''' </summary>
''' <remarks>
''' Draws the text itself via FormattedText/BuildHighlightGeometry instead of a TextBlock with per-Run
''' Background colors. Run.Background always paints the full font-defined line box (ascent + descent), which
''' for most fonts reserves noticeably more space above cap-height than below the baseline, making a
''' Run-based highlight look asymmetric/top-heavy no matter how its surrounding spacing is tuned.
''' BuildHighlightGeometry instead produces a tight rectangle around the actual matched glyphs, avoiding that
''' asymmetry entirely - the same mechanism WPF's own text-selection highlighting is built on.
''' </remarks>
'''
Public Class HighlightedSnippetView

    Inherits FrameworkElement


    Public Shared ReadOnly TextProperty As DependencyProperty =
        DependencyProperty.Register("Text", GetType(String), GetType(HighlightedSnippetView),
        New FrameworkPropertyMetadata(Nothing, FrameworkPropertyMetadataOptions.AffectsMeasure Or FrameworkPropertyMetadataOptions.AffectsRender))

    Public Property Text As String
        Get
            Return CStr(GetValue(TextProperty))
        End Get
        Set(value As String)
            SetValue(TextProperty, value)
        End Set
    End Property


    Public Shared ReadOnly ForegroundProperty As DependencyProperty =
        DependencyProperty.Register("Foreground", GetType(Brush), GetType(HighlightedSnippetView),
        New FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender))

    Public Property Foreground As Brush
        Get
            Return DirectCast(GetValue(ForegroundProperty), Brush)
        End Get
        Set(value As Brush)
            SetValue(ForegroundProperty, value)
        End Set
    End Property


    Public Shared ReadOnly FontFamilyProperty As DependencyProperty =
        DependencyProperty.Register("FontFamily", GetType(FontFamily), GetType(HighlightedSnippetView),
        New FrameworkPropertyMetadata(New FontFamily("Segoe UI"), FrameworkPropertyMetadataOptions.AffectsMeasure Or FrameworkPropertyMetadataOptions.AffectsRender))

    Public Property FontFamily As FontFamily
        Get
            Return DirectCast(GetValue(FontFamilyProperty), FontFamily)
        End Get
        Set(value As FontFamily)
            SetValue(FontFamilyProperty, value)
        End Set
    End Property


    Public Shared ReadOnly FontSizeProperty As DependencyProperty =
        DependencyProperty.Register("FontSize", GetType(Double), GetType(HighlightedSnippetView),
        New FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsMeasure Or FrameworkPropertyMetadataOptions.AffectsRender))

    Public Property FontSize As Double
        Get
            Return CDbl(GetValue(FontSizeProperty))
        End Get
        Set(value As Double)
            SetValue(FontSizeProperty, value)
        End Set
    End Property


    Private Shared ReadOnly HighlightBrush As Brush = Brushes.Yellow
    Private Shared ReadOnly HighlightForeground As Brush = Brushes.Black


    ''' <summary>
    ''' Extra horizontal breathing room drawn on either side of each highlighted rectangle, in device-independent
    ''' pixels.
    ''' </summary>
    '''
    Private Const HorizontalHighlightPadding As Double = 2.0


    Protected Overrides Function MeasureOverride(availableSize As Size) As Size

        If String.IsNullOrEmpty(Text) Then
            Return New Size(0, 0)
        End If

        Dim widthConstraint = If(Double.IsPositiveInfinity(availableSize.Width), 10000, availableSize.Width)
        Dim formattedText = CreateFormattedText(widthConstraint)

        Return New Size(Math.Min(formattedText.WidthIncludingTrailingWhitespace, widthConstraint), formattedText.Height)

    End Function


    Protected Overrides Sub OnRender(drawingContext As DrawingContext)

        If String.IsNullOrEmpty(Text) Then
            Return
        End If

        Dim formattedText = CreateFormattedText(ActualWidth)

        Dim plainText As String = Nothing
        Dim ranges = ParseHighlightRanges(Text, plainText)

        For Each highlightRange In ranges
            Dim geometry = formattedText.BuildHighlightGeometry(New Point(0, 0), highlightRange.Start, highlightRange.Length)
            If geometry IsNot Nothing Then
                DrawPaddedHighlight(drawingContext, geometry)
            End If
        Next

        drawingContext.DrawText(formattedText, New Point(0, 0))

    End Sub


    ''' <summary>
    ''' Draws the given highlight geometry as one or more plain rectangles, each inflated by
    ''' <see cref="HorizontalHighlightPadding"/> on the left/right - rather than drawing the geometry's exact
    ''' shape as-is - so the highlight doesn't hug the matched text quite so tightly. A highlighted phrase that
    ''' wraps across lines produces a separate rectangle per line (a GeometryGroup), each padded independently,
    ''' so padding is never added across the gap between two wrapped lines.
    ''' </summary>
    '''
    Private Shared Sub DrawPaddedHighlight(drawingContext As DrawingContext, geometry As Geometry)

        Dim group = TryCast(geometry, GeometryGroup)

        If group IsNot Nothing Then
            For Each child In group.Children
                drawingContext.DrawRectangle(HighlightBrush, Nothing, Rect.Inflate(child.Bounds, HorizontalHighlightPadding, 0))
            Next
        Else
            drawingContext.DrawRectangle(HighlightBrush, Nothing, Rect.Inflate(geometry.Bounds, HorizontalHighlightPadding, 0))
        End If

    End Sub


    Private Function CreateFormattedText(maxWidth As Double) As FormattedText

        Dim plainText As String = Nothing
        Dim ranges = ParseHighlightRanges(Text, plainText)

        Dim pixelsPerDip = VisualTreeHelper.GetDpi(Me).PixelsPerDip

        Dim formattedText As New FormattedText(plainText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            New Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), FontSize, Foreground, pixelsPerDip) With {
            .MaxTextWidth = Math.Max(maxWidth, 0),
            .Trimming = TextTrimming.None
        }

        For Each highlightRange In ranges
            formattedText.SetForegroundBrush(HighlightForeground, highlightRange.Start, highlightRange.Length)
        Next

        Return formattedText

    End Function


    ''' <summary>
    ''' Strips the highlight marker characters out of the raw snippet, returning the plain text via
    ''' <paramref name="plainText"/> and the (start, length) character ranges - within that plain text - that
    ''' were wrapped in markers.
    ''' </summary>
    '''
    Private Function ParseHighlightRanges(rawText As String, ByRef plainText As String) As List(Of (Start As Integer, Length As Integer))

        Dim builder As New Text.StringBuilder
        Dim ranges As New List(Of (Start As Integer, Length As Integer))
        Dim rangeStart = -1

        For Each c In If(rawText, "")

            If c = FullTextSearch.HighlightStartMarker Then
                rangeStart = builder.Length
            ElseIf c = FullTextSearch.HighlightEndMarker Then
                If rangeStart >= 0 Then
                    ranges.Add((rangeStart, builder.Length - rangeStart))
                    rangeStart = -1
                End If
            Else
                builder.Append(c)
            End If

        Next

        plainText = builder.ToString()
        Return ranges

    End Function

End Class

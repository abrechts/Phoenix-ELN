'
' <summary>
'' Implements a document page template with adjustable headers, content and footers. If individual controls
'' on the print content implement its powerful paginator interface IVisualPaginator.PageBreakBoundaryOffset,   
'' they report back where a page break can occur within them.
'' Important: The content to be paginated is expected to be contained in a StackPanel.
'' </summary>

Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Documents
Imports System.Windows.Media
Imports System.Windows.Media.Imaging

Public Class PrintPageTemplate

    Private mShrinkFactor As Double

    Public Sub New(printPanel As Panel, printTicket As Printing.PrintTicket, shrinkFactor As Double)

        ' This call is required by the designer.
        InitializeComponent()

        mShrinkFactor = shrinkFactor

        With printTicket
            If .PageOrientation = Printing.PageOrientation.Portrait Then
                PageSize = New Size(.PageMediaSize.Width, .PageMediaSize.Height)
            Else
                PageSize = New Size(.PageMediaSize.Height, .PageMediaSize.Width)
            End If
        End With

        PrintScroller.Content = printPanel

        blkFooterLeft.Text = ""
        blkFooterCenter.Text = ""
        blkFooterRight.Text = ""
        blkHeaderLeft.Text = ""
        blkHeaderCenter.Text = ""
        blkHeaderRight.Text = ""
        blkConfidential.HorizontalAlignment = HorizontalAlignment.Center

        pnlFooterImg.Visibility = Visibility.Collapsed
        blkConfidential.Visibility = Visibility.Collapsed

    End Sub


    Public Property ShowConfidentialMarker As Boolean
        Get
            Return (blkConfidential.Visibility = Visibility.Visible)
        End Get
        Set(value As Boolean)
            If value Then
                blkConfidential.Visibility = Visibility.Visible
            End If
        End Set
    End Property


    Public Property PageSize As Size


    ''' <summary>
    ''' Gets the custom paginator, which considers the interface function 'PageBreakBoundaryOffset' if 
    ''' implemented in any of the expected content StackPanel children. If present in a child element, 
    ''' and if the element is within a page break region, the page break location proposed by the 
    ''' element is taken.  
    ''' </summary>
    ''' 
    Public ReadOnly Property Paginator As VisualPaginator
        Get
            Paginator = New VisualPaginator(Me, mShrinkFactor)
        End Get
    End Property


    ''' <summary>
    ''' Sets or gets the print borders inside the printable area. 
    ''' </summary>
    ''' <value>Thickness defining border areas. Default is (50,0,50,0) </value>
    ''' 
    Public Property BorderArea As Thickness
        Get
            Return MainGrid.Margin
        End Get
        Set(value As Thickness)
            MainGrid.Margin = value
        End Set
    End Property


    ''' <summary>
    ''' Sets or gets if the page header is visible or not.
    ''' </summary>
    ''' 
    Public Property IsHeaderVisible As Boolean
        Get
            Return HeaderRow.MaxHeight <> 0
        End Get
        Set(value As Boolean)
            HeaderBorder.MaxHeight = If(value, Double.PositiveInfinity, 0)
        End Set
    End Property


    ''' <summary>
    ''' Sets or gets if the page footer is visible.
    ''' </summary>
    '''
    Public Property IsFooterVisible As Boolean
        Get
            Return FooterBorder.MaxHeight <> 0
        End Get
        Set(value As Boolean)
            FooterBorder.MaxHeight = If(value, Double.PositiveInfinity, 0)
            pnlFooterImg.Visibility = If(value AndAlso Not IsNothing(FooterCenterImage), Visibility.Visible, Visibility.Collapsed)
        End Set
    End Property


    ''' <summary>
    ''' Sets or gets the left header title text.
    ''' </summary>
    '''
    Public Property HeaderTitleLeft As String
        Get
            Return blkHeaderLeft.Text
        End Get
        Set(value As String)
            blkHeaderLeft.Text = value
        End Set
    End Property


    ''' <summary>
    ''' Sets or gets the center header title text.
    ''' </summary>
    '''
    Public Property HeaderTitleCenter As String
        Get
            Return blkHeaderCenter.Text
        End Get
        Set(value As String)
            blkHeaderCenter.Text = value
            blkConfidential.HorizontalAlignment = HorizontalAlignment.Right
        End Set
    End Property


    ''' <summary>
    ''' Sets or gets the right header title text.
    ''' </summary>
    '''
    Public Property HeaderTitleRight As String
        Get
            Return blkHeaderRight.Text
        End Get
        Set(value As String)
            blkHeaderRight.Text = value
        End Set
    End Property


    ''' <summary>
    ''' Sets or gets the center footer bitmap image.
    ''' </summary>
    '''
    Public Property FooterCenterImage As BitmapImage
        Get
            Return imgFooterCenter.Source
        End Get
        Set(value As BitmapImage)
            imgFooterCenter.Source = value
            pnlFooterImg.Visibility = If(IsNothing(value) OrElse Not IsFooterVisible, Visibility.Collapsed, Visibility.Visible)
        End Set
    End Property


    ''' <summary>
    ''' Sets or gets the left footer title text.
    ''' </summary>
    '''
    Public Property FooterTitleLeft As String
        Get
            Return blkFooterLeft.Text
        End Get
        Set(value As String)
            blkFooterLeft.Text = value
        End Set
    End Property


    'use for writing page numbers via document paginator only!
    Friend Property FooterTitleCenter As String
        Get
            Return blkFooterCenter.Text
        End Get
        Set(value As String)
            blkFooterCenter.Text = value
        End Set
    End Property


    ''' <summary>
    ''' Sets or gets the right header title text.
    ''' </summary>
    '''
    Public Property FooterTitleRight As String
        Get
            Return blkFooterRight.Text
        End Get
        Set(value As String)
            blkFooterRight.Text = value
        End Set
    End Property


    ''' <summary>
    ''' 
    ''' Provides print pagination logic for StackPanels, allowing correct page breaks for its children 
    ''' whenever they implement the interface IVisualPaginator. If this interface is not implemented by 
    ''' an element, the page break location is not corrected and can occur anywhere within the element.
    ''' 
    ''' At the core of IVisualPaginator is the function PageBreakBoundaryOffset, where each individual control 
    ''' can determine where a page break within its bounds are appropriate.
    ''' 
    ''' </summary>

    Public Class VisualPaginator

        Inherits DocumentPaginator

        Private mTemplate As PrintPageTemplate
        Private mStackScroller As New ScrollViewer
        Private mPageSize As Size
        Private mPageTotal As Integer
        Private mPageNumber As Integer
        Private mIsPageCountValid As Boolean = False
        Private mPage As DocumentPage
        Private mYOffset As Double
        Private mPageOffsets As New List(Of Double)
        Private mVisBrush As VisualBrush

        ''' <summary>
        ''' This interface is implemented by custom controls which provide their own logic for 
        ''' correcting page breaks occurring within their boundaries.
        ''' </summary>
        ''' 
        Interface IVisualPaginator

            Function PageBreakBoundaryOffset(proposedPageBreakY As Double) As Double

        End Interface


        Friend Sub New(stackPrintTemplate As PrintPageTemplate, shrinkFactor As Double)

            mPageSize = stackPrintTemplate.PageSize
            mTemplate = stackPrintTemplate

            With stackPrintTemplate
                .Width = .PageSize.Width
                .Height = .PageSize.Height
                .Measure(.PageSize)
                .Arrange(New Rect(New Point(0, 0), .PageSize))
                .RenderTransform = New ScaleTransform(shrinkFactor, shrinkFactor, .Width / 2, .Height / 2)
                .UpdateLayout()
            End With

            Dim printStackPanel = stackPrintTemplate.PrintScroller.Content
            mStackScroller = stackPrintTemplate.PrintScroller
            mStackScroller.UpdateLayout()

            mPageTotal = InitPageCount(printStackPanel)

            mPageNumber = mPageTotal
            mIsPageCountValid = False  'if true, then pageCount returns total number of pages

            'allows to scroll down to page bottom, not only to end of panel content
            With printStackPanel
                .Height = mPageTotal * PageSize.Height
                .UpdateLayout()
            End With

        End Sub


        ''' <summary>
        ''' Gets the number of pixels the proposedLowerBoundary must be reduced to allow controlled splitting of  
        ''' a printPanel child by a page break.
        ''' </summary>
        ''' <param name="printPanel">The vertical StackPanel containing the children to print.</param>
        ''' <param name="proposedLowerBoundary">The current y-coordinate of the end of the current page in printPanel coordinates. </param>
        ''' <returns></returns>
        ''' 
        Private Function GetPageBreakOffsetDiff(printPanel As StackPanel, proposedLowerBoundary As Double) As Double

            Dim yOffsetCorrect As Double = 0
            Dim ctrlPos As Point

            If TypeOf printPanel Is StackPanel Then

                For Each ctrl As FrameworkElement In CType(printPanel, StackPanel).Children

                    'proposed page break somewhere *inside* control?
                    '-------------------------------------------------
                    ctrlPos = ctrl.TranslatePoint(Nothing, CType(printPanel, StackPanel))
                    If ctrlPos.Y < proposedLowerBoundary AndAlso (ctrlPos.Y + ctrl.ActualHeight) > proposedLowerBoundary Then
                        If TypeOf ctrl Is IVisualPaginator Then
                            'these controls expose custom page break logic
                            Dim inCtrlPos = printPanel.TranslatePoint(New Point(0, proposedLowerBoundary), ctrl)  'gets the boundary y in child coordinates. 
                            yOffsetCorrect = CType(ctrl, IVisualPaginator).PageBreakBoundaryOffset(inCtrlPos.Y) - 1
                        Else
                            ' some default corrections for standard objects like data grid etc. might be implemented here in the future
                        End If
                    End If

                Next

                Return yOffsetCorrect

            Else
                Return 0
            End If

        End Function


        Public Overrides Function GetPage(pageNumber As Integer) As System.Windows.Documents.DocumentPage

            mPageNumber = pageNumber
            If pageNumber = (mPageTotal - 1) Then
                mIsPageCountValid = True
            End If

            Dim clipHeight As Double = If(pageNumber < (mPageTotal - 1), mPageOffsets(pageNumber + 1) - mPageOffsets(pageNumber), PageSize.Height)
            Dim clipRect As New RectangleGeometry(New Rect(New Point(0, 0), New Point(PageSize.Width, clipHeight)))

            With mStackScroller
                .ScrollToVerticalOffset(mPageOffsets(pageNumber))
                .Clip = clipRect
                .UpdateLayout()
            End With

            Dim contentBox = New Rect
            With contentBox
                .Width = PageSize.Width
                .Height = PageSize.Height
                .X = 0
                .Y = 0
            End With

            mPageNumber = pageNumber

            mTemplate.FooterTitleCenter = "- Page " + (mPageNumber + 1).ToString + "/" + mPageTotal.ToString + " -"
            mTemplate.UpdateLayout()

            mPage = New DocumentPage(mTemplate, PageSize, contentBox, contentBox)

            Return mPage

        End Function

        Public Overrides ReadOnly Property IsPageCountValid As Boolean
            Get
                Return mIsPageCountValid
            End Get
        End Property

        Public Overrides ReadOnly Property PageCount As Integer
            Get
                Return mPageTotal
            End Get
        End Property

        Public Overrides Property PageSize As System.Windows.Size
            Get
                Return mPageSize
            End Get
            Set(value As System.Windows.Size)
                mPageSize = PageSize
            End Set
        End Property

        Public Overrides ReadOnly Property Source As System.Windows.Documents.IDocumentPaginatorSource
            Get
                Return mPage
            End Get
        End Property


        Private Function InitPageCount(printStack As StackPanel) As Integer

            Dim yOffset As Double = 0
            Dim yOffsetCorrect As Double
            Dim pageCount As Integer = 0
            Dim scrollerHeight = mStackScroller.ActualHeight

            While yOffset < printStack.ActualHeight
                yOffsetCorrect = GetPageBreakOffsetDiff(printStack, yOffset + scrollerHeight)
                mPageOffsets.Add(yOffset)
                yOffset += scrollerHeight + yOffsetCorrect
                pageCount += 1
            End While

            Return pageCount

        End Function

    End Class

End Class



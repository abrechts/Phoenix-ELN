
Imports System.Windows.Controls
Imports System.Windows.Media
Imports System.Windows
Imports ElnCoreModel


Public Class SequenceStructure

    Public Shared Event StepArrowClicked(sender As Object)

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        ArrowColor = Brushes.Lavender
        UpperLabelForeground = Brushes.LightBlue
        LowerLabelForeground = Brushes.WhiteSmoke

    End Sub


    ''' <summary>
    ''' Sets or gets the sequential step number string (to be assigned when populating the scheme)
    ''' </summary>
    ''' 
    Public Property StepNumberStr As String
        Get
            Return blkStepNr.Text
        End Get
        Set(value As String)
            blkStepNr.Text = value
        End Set
    End Property


    ''' <summary>
    ''' Sets or gets the foreground brush of the reaction arrow
    ''' </summary>
    ''' 
    Public Property ArrowColor As SolidColorBrush

        Get
            Return CType(TryFindResource("arrowForegroundCol"), SolidColorBrush)
        End Get
        Set(value As SolidColorBrush)
            CType(TryFindResource("arrowForegroundCol"), SolidColorBrush).Color = value.Color
        End Set

    End Property


    Public Property UpperLabelForeground As SolidColorBrush

        Get
            Return blkStepNr.Foreground
        End Get
        Set(value As SolidColorBrush)
            blkStepNr.Foreground = value
        End Set

    End Property


    Public Property LowerLabelForeground As SolidColorBrush

        Get
            Return blkExpCount.Foreground
        End Get
        Set(value As SolidColorBrush)
            blkExpCount.Foreground = value
        End Set

    End Property



    ''' <summary>
    ''' Sets or gets the canvas of the step reactant or product structure
    ''' </summary>
    ''' 
    Public Property StructureCanvas As Canvas

        Get
            Return vboxReactantStructure.Child
        End Get

        Set(value As Canvas)

            vboxReactantStructure.Child = value
            ScaleViewBoxSize(vboxReactantStructure, 150)

        End Set

    End Property


    ''' <summary>
    ''' Sets or gets the border brush of the step selection border
    ''' </summary>
    ''' 
    Public Property SelectionBorderBrush As Brush = Brushes.LightSkyBlue


    Private _IsSelected As Boolean = False


    ''' <summary>
    ''' Sets or gets if the reaction arrow is marked as as selected
    ''' </summary>
    '''
    Public Property IsSelected As Boolean

        Get
            Return _IsSelected
        End Get

        Set(value As Boolean)

            _IsSelected = value

            If value = True Then
                pnlArrowRight.BorderBrush = SelectionBorderBrush
            Else
                pnlArrowRight.BorderBrush = Brushes.Transparent
                pnlArrowRight.Background = Brushes.Transparent
            End If

        End Set

    End Property


    ''' <summary>
    ''' Populates StepExperiments with all experiments containing the same reaction as the specified reference experiment
    ''' </summary>
    '''
    Public Sub SetStepExperiments(refExp As tblExperiments, expList As IQueryable(Of tblExperiments))

        Dim sameStepExp = expList.Where(Function(n) _
               (n.ReactantInChIKey = refExp.ReactantInChIKey AndAlso n.IsRacemicReactant = refExp.IsRacemicReactant) AndAlso
               (n.ProductInChIKey = refExp.ProductInChIKey AndAlso n.IsRacemicProduct = refExp.IsRacemicProduct))

        StepExperiments = sameStepExp.OrderByDescending(Function(n) n.Yield)

    End Sub

    ''' <summary>
    ''' Sets or gets the list of experiment items represented by this step
    ''' </summary>
    ''' 
    Public Property StepExperiments As IQueryable(Of tblExperiments)


    ''' <summary>
    ''' Hides the right solid arrow
    ''' </summary>
    ''' 
    Public Sub HideRightArrow()

        pnlArrowRight.Visibility = Visibility.Collapsed

    End Sub


    ''' <summary>
    ''' Displays the dotted right arrow
    ''' </summary>
    ''' 
    Public Sub ShowDottedRightArrow()

        HideRightArrow()
        arrowRightDotted.Visibility = Visibility.Visible

    End Sub


    ''' <summary>
    ''' Displays the left arrow
    ''' </summary>
    ''' 
    Public Sub ShowLeftArrow()

        arrowLeftDotted.Visibility = Visibility.Visible

    End Sub


    ''' <summary>
    ''' Scales the summary compound ViewBox in a way to obtain an uniform bond length across all components.
    ''' </summary>
    ''' <param name="cpdView">ViewBox containing the components.</param>
    ''' 
    Private Sub ScaleViewBoxSize(ByRef cpdView As Viewbox, maxWidth As Double)

        With CType(cpdView.Child, Canvas)

            Dim scaleFactor = 0.21 '0.19

            cpdView.Height = scaleFactor * .Height
            cpdView.Width = scaleFactor * .Width

        End With

    End Sub


    Private Sub pnlArrowRight_PreviewMouseUp() Handles pnlArrowRight.PreviewMouseUp

        RaiseEvent StepArrowClicked(Me)

    End Sub


End Class

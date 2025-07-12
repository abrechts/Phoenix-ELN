
Imports System.Windows.Controls
Imports System.Windows.Media
Imports System.Windows


Public Class SequenceStructure

    Public Shared Event StepArrowSelected(sender As Object)

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
                pnlArrowRight.BorderBrush = Brushes.LightSkyBlue
            Else
                If SourceStep IsNot Nothing Then
                    If Not SourceStep.IsSeedStep Then
                        pnlArrowRight.BorderBrush = Brushes.Transparent
                        pnlArrowRight.Background = Brushes.Transparent
                    Else
                        pnlArrowRight.BorderBrush = Brushes.Gray
                        pnlArrowRight.Background = CType(New BrushConverter().ConvertFrom("#FF3F3F3F"), SolidColorBrush)
                    End If
                End If
            End If

        End Set

    End Property


    ''' <summary>
    ''' Sets or gets the underlying SequenceStep
    ''' </summary>
    ''' 
    Public Property SourceStep As SequenceStep


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


    ''' <summary>
    ''' Gets if the right arrow element is not connecting to the next sequence structure, but indicating 
    ''' that the sequence is connecting to other sequences downstream.
    ''' </summary>
    ''' 
    Private Function IsTrailingRightArrow() As Boolean

        Dim parentPanel = WPFToolbox.FindVisualParent(Of WrapPanel)(Me)
        If parentPanel IsNot Nothing Then
            Return (parentPanel.Children.IndexOf(Me) = parentPanel.Children.Count - 1)
        Else
            Return False
        End If

    End Function



    Private Sub pnlArrowRight_PreviewMouseDown() Handles pnlArrowRight.PreviewMouseDown

        If Not IsTrailingRightArrow() Then
            RaiseEvent StepArrowSelected(Me)
        End If

    End Sub

End Class


Imports System.Windows.Controls
Imports System.Windows.Media
Imports System.Windows

Public Class SequenceStructure

    Public Shared Event StepArrowSelected(sender As Object)

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


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
                pnlArrowRight.BorderBrush = CType(New BrushConverter().ConvertFrom("#FF7DDEFF"), SolidColorBrush)
                pnlArrowRight.Background = CType(New BrushConverter().ConvertFrom("#FFD8F5FF"), SolidColorBrush)
            Else
                pnlArrowRight.BorderBrush = Brushes.Transparent
                pnlArrowRight.Background = Brushes.Transparent
            End If

        End Set

    End Property


    Private Sub pnlArrowRight_MouseEnter() Handles pnlArrowRight.MouseEnter

        pnlArrowRight.Background = CType(New BrushConverter().ConvertFrom("#FFD8F5FF"), SolidColorBrush)

    End Sub


    Private Sub pnlArrowRight_MouseLeave() Handles pnlArrowRight.MouseLeave

        If Not IsSelected Then
            pnlArrowRight.Background = Brushes.Transparent
        End If

    End Sub



    ''' <summary>
    ''' Sets or gets the underlying SequenceStep
    ''' </summary>
    ''' 
    Public Property SourceStep As SequenceStep



    Public Sub HideRightArrow()

        arrowRight.Visibility = Visibility.Collapsed

    End Sub


    Public Sub ShowDottedRightArrow()

        HideRightArrow()
        arrowRightDotted.Visibility = Visibility.Visible

    End Sub


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

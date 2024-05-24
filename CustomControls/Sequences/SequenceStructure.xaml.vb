
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

            IsStructureSelected = value

            If value = True Then
                arrowRight.Visibility = Visibility.Collapsed
                pnlArrowRightHighlighted.Visibility = Visibility.Visible
            Else
                arrowRight.Visibility = Visibility.Visible
                pnlArrowRightHighlighted.Visibility = Visibility.Collapsed
            End If

            'also highlight next reactant structure

            Dim parentPanel = WPFToolbox.FindVisualParent(Of WrapPanel)(Me)
            If parentPanel IsNot Nothing Then

                Dim currIndex = parentPanel.Children.IndexOf(Me)
                If currIndex < parentPanel.Children.Count - 1 Then
                    Dim nextStepStruct = CType(parentPanel.Children(currIndex + 1), SequenceStructure)
                    nextStepStruct.IsStructureSelected = True
                End If

            End If

        End Set

    End Property


    ''' <summary>
    ''' Sets if the reactant structure are is selected (highlighted)
    ''' </summary>
    '''
    Public WriteOnly Property IsStructureSelected As Boolean

        Set(value As Boolean)
            If value = True Then
                pnlReactantStructure.Background = CType(New BrushConverter().ConvertFrom("#FFE9F7FF"), SolidColorBrush)
                pnlReactantStructure.BorderBrush = Brushes.LightBlue
            Else
                pnlReactantStructure.Background = Brushes.Transparent
                pnlReactantStructure.BorderBrush = Brushes.Transparent
            End If
        End Set

    End Property


    ''' <summary>
    ''' Sets or gets the underlying SequenceStep
    ''' </summary>
    ''' 
    Public Property SourceStep As SequenceStep



    Public Sub HideRightArrow()

        pnlArrowRight.Visibility = Windows.Visibility.Collapsed

    End Sub


    Public Sub ShowLeftArrow()

        pnlArrowLeft.Visibility = Windows.Visibility.Visible

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


    Private Sub pnlArrowRight_MouseEnter() Handles pnlArrowRight.MouseEnter

        If Not IsTrailingRightArrow() Then
            pnlArrowRight.BorderBrush = Brushes.Blue
        End If

    End Sub


    Private Sub pnlArrowRight_MouseLeave() Handles pnlArrowRight.MouseLeave

        pnlArrowRight.BorderBrush = Brushes.Transparent

    End Sub


    Private Sub pnlArrowRight_PreviewMouseDown() Handles pnlArrowRight.PreviewMouseDown

        If Not IsTrailingRightArrow() Then
            RaiseEvent StepArrowSelected(Me)
        End If

    End Sub

End Class

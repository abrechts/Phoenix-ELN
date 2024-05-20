Imports System.Windows.Controls

Public Class SequenceStructure

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


End Class

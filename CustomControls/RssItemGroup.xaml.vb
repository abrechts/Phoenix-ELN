Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports ElnCoreModel

Public Class RssItemGroup

    Public Shared Event RequestOpenExperiment(sender As Object, expEntry As tblExperiments)


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.

    End Sub


    Private Sub lstRssHits_PreviewMouseUp(sender As Object, e As MouseButtonEventArgs) Handles lstRssHits.PreviewMouseUp

        Dim selItem = lstRssHits.SelectedItem
        RaiseEvent RequestOpenExperiment(Me, lstRssHits.SelectedItem)

    End Sub


    Private Sub lstRssHits_LostFocus() Handles lstRssHits.LostFocus

        lstRssHits.UnselectAll

    End Sub



    Private Sub Canvas_DataContextChanged(sender As Object, e As DependencyPropertyChangedEventArgs)

        ScaleViewBoxSize(sender.parent, 150)

    End Sub


    ''' <summary>
    ''' Scales the summary compound ViewBox in a way to obtain an uniform bond length across all components.
    ''' </summary>
    ''' <param name="cpdView">ViewBox containing the components.</param>
    ''' 
    Private Sub ScaleViewBoxSize(ByRef cpdView As Viewbox, maxWidth As Double)

        With CType(CType(cpdView.Child, ContentPresenter).Content, Canvas)

            Dim scaleFactor = 0.19

            cpdView.Height = scaleFactor * .Height
            cpdView.Width = scaleFactor * .Width

        End With

    End Sub

End Class


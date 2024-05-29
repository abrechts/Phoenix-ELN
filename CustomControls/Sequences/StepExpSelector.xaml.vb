Imports ElnCoreModel
Imports System.Windows.Data
Imports System.Windows.Input

Public Class StepExpSelector

    'DataContext is SequenceStep

    Public Shared Event RequestOpenExperiment(sender As Object, expEntry As tblExperiments, isFromServer As Boolean)


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.

    End Sub


    ''' <summary>
    ''' The DataContext of this control is SequenceStep
    ''' </summary>
    ''' 
    Private Sub Me_DataContextChanged() Handles Me.DataContextChanged

        Dim seqStep = CType(Me.DataContext, SequenceStep)

        Dim res = From exp In seqStep.GetStepExperiments Order By exp.Yield Descending
        lstStepExperiments.ItemsSource = res

    End Sub


    Private Sub ListBoxItem_Selected(sender As Object, e As MouseButtonEventArgs) Handles lstStepExperiments.PreviewMouseUp

        Dim selExp = lstStepExperiments.SelectedItem

        If selExp IsNot Nothing Then
            lstStepExperiments.UnselectAll()
            RaiseEvent RequestOpenExperiment(Me, selExp, False)
        End If

    End Sub


    'Private Sub cboSortType_Changed() Handles cboSortType.SelectionChanged

    '    If Me.IsInitialized Then

    '        With cvsStepExperiments

    '            .SortDescriptions.Clear()

    '            Select Case cboSortType.SelectedIndex

    '                Case 0  'by yield
    '                    .SortDescriptions.Add(New SortDescription("Yield", ListSortDirection.Descending))

    '                Case 1 'by scale
    '                    .SortDescriptions.Add(New SortDescription("RefReactantGrams", ListSortDirection.Descending))

    '            End Select

    '            cvsStepExperiments.View.Refresh()

    '        End With

    '    End If

    'End Sub



End Class

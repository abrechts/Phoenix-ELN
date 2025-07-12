Imports System.Globalization
Imports System.Windows.Data
Imports System.Windows.Input
Imports ElnBase.ELNEnumerations
Imports ElnCoreModel

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

        ExpEntryEnabledConverter.IsServerExperiments = (dlgSequences.UseServerContext AndAlso dlgSequences.ServerContext IsNot Nothing)

        'get all step experiments; unfinalized server experiments will be disabled later in the step exp list 
        Dim res = From exp In seqStep.GetStepExperiments Order By exp.Yield Descending
        lstStepExperiments.ItemsSource = res

    End Sub


    Private Sub ListBoxItem_Selected(sender As Object, e As MouseButtonEventArgs) Handles lstStepExperiments.PreviewMouseUp

        Dim selExp As tblExperiments = lstStepExperiments.SelectedItem

        If selExp IsNot Nothing Then
            lstStepExperiments.UnselectAll()
            RaiseEvent RequestOpenExperiment(Me, selExp, False)
        End If

    End Sub


End Class


Public Class ExpEntryEnabledConverter

    Implements IValueConverter

    Public Shared Property IsServerExperiments As Boolean = False

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim exp As tblExperiments = value

        If IsServerExperiments AndAlso Not exp.WorkflowState = WorkflowStatus.Finalized Then
            Return False
        Else
            Return True
        End If

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function

End Class

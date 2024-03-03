Imports ElnBase

Public Class dlgSearch

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.

    End Sub



    Public Property SourceSketchRxn As String

    Public Property QuerySketchRxn As String


    Private Sub PerformMatch()

        Dim rxnSub As New RxnSubstructure

        If rxnSub.MatchReaction(SourceSketchRxn, QuerySketchRxn) Then
            MsgBox("Hit!")
        Else
            MsgBox("No Hit!")
        End If

    End Sub


    Private Sub pnlQuerySketch_SketchInfoAvailable(sender As Object, skInfo As SketchResults) Handles pnlQuerySketch.SketchInfoAvailable

        If sender.Name = "pnlFullSketch" Then

            SourceSketchRxn = skInfo.MDLRxnFileString

        Else

            QuerySketchRxn = skInfo.MDLRxnFileString
            If SourceSketchRxn <> "" Then
                PerformMatch()
                Dim x = 1
            End If

        End If

    End Sub




End Class

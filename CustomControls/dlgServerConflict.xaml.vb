Imports ElnBase
Imports ElnCoreModel

Public Class dlgServerConflict


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    ''' <summary>
    ''' If false, then the Rename strategy was selected.
    ''' </summary>
    ''' 
    Public Property UseRestoreStrategy As Boolean


    Private Sub btnOK_Click() Handles btnOK.Click

        UseRestoreStrategy = rdoRestore.IsChecked
        Me.Close()

    End Sub


End Class

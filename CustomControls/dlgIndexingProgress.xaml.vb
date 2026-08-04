''' <summary>
''' Non-interactive "please wait" indicator shown while the full-text SearchIndex is being backfilled at
''' startup for a database large enough that the (synchronous) rebuild would otherwise make the app appear
''' to hang. The caller is responsible for calling Show(), pumping the dispatcher once so this actually
''' paints before the blocking rebuild starts, and Close()ing it afterwards.
''' </summary>
'''
Public Class dlgIndexingProgress

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub

End Class

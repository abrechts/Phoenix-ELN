''' <summary>
''' Shared constants/helpers for the experiment search dialogs (dlgFullTextSearch, dlgMaterialSearch,
''' dlgTagSearch) that list matching experiments.
''' </summary>
'''
Public Class SearchBase

    ''' <summary>
    ''' Upper bound on the number of experiments displayed by an experiment-listing search, so an overly
    ''' broad query (e.g. a common full-text word, or a material like water that occurs in nearly every
    ''' workup) can't flood the results list with every experiment in the database.
    ''' </summary>
    '''
    Public Shared ReadOnly MaxDisplayedResults As Integer = 200


    ''' <summary>
    ''' Builds the "N experiment(s) found[ - showing the top X best matches only]" text shown below an
    ''' experiment search's results list.
    ''' </summary>
    '''
    Public Shared Function BuildHitCountText(hitCount As Integer, wasTruncated As Boolean) As String

        Return $"{hitCount} experiment(s) found" +
            If(wasTruncated, $" - showing the {MaxDisplayedResults} best matches only", "")

    End Function

End Class

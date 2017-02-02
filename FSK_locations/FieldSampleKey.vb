''' <summary>
'''   $Header: EDP/Formats/ESBasic/ESBasic.vb  21  2015-01-19 13:47:31-07:00  Bryce Mathews <bryce.mathews@earthsoft.com> $
'''		$UTCDate: 2010-09-16 14:47:57Z $
''' </summary>
Option Strict Off

Imports EarthSoft.Common
Imports EarthSoft.Edp
Imports System
Imports System.Collections
Imports System.Runtime.InteropServices
Imports System.Reflection

<Assembly: AssemblyCompany("EarthSoft, Inc.")>
<Assembly: AssemblyProduct("EQuIS")>
<Assembly: AssemblyCopyright("Copyright © 2002-2015, EarthSoft, Inc.")>
<Assembly: AssemblyTrademark("")>
<Assembly: AssemblyVersion("3.0.20")> 

Public Class FieldSampleKeyHandler
  Inherits EarthSoft.EDP.EddCustomHandler
  Private _OpenDialog As Object
  Private pkg As EddPackage

    Private _efd As EddFormatDefinition

   Private sampleRow As System.Data.DataRow

    Private FieldSampleKey As System.Data.DataTable
    Private Locations As System.Data.DataTable




	Public Sub New()
	
	End Sub

	Public Overrides Sub AddDataHandlers(ByRef Efd As EarthSoft.EDP.EddFormatDefinition)
    _efd = Efd


        Me.FieldSampleKey = Efd.Tables.Item("FieldSampleKey")
        Me.Locations = Efd.Tables.Item("Locations")	

	    AddHandler FieldSampleKey.ColumnChanged, AddressOf Me.FieldSampleKey_ColumnChanged
		AddHandler Locations.ColumnChanged, AddressOf Me.Locations_ColumnChanged

	End Sub
Private Sub BeforeDataLoad(ByVal eddTable As EarthSoft.EDP.EddTable)
    Me.sampleRow = Nothing
  End Sub

	Public Overloads Overrides Function ErrorMessage(ByVal err As EddErrors) As String
    Select Case err
      Case EddErrors.CustomError1
        Return "Parent sample code is required for the associated sample type."
      Case EddErrors.CustomError4
        Return "Start Depth required if matrix_code = 'SO' or 'SE'."
      Case EddErrors.CustomError5
        Return "Start Depth must be less than End Depth."

    End Select

    Return String.Empty
	End Function

	'KP_Case11563_20060703
	Public Sub Locations_ColumnChanged(ByVal sender As Object, ByVal e As System.Data.DataColumnChangeEventArgs)
		'this function is called whenever the user changes a value,
		'so you can check for custom errors, etc.
	    'Select Case e.Column.ColumnName.ToLower
          '
          'Case "sample_type_code"
           ' ERR01(e)        'Parent_sample_code is required if rt_sample_type.needs_parent_sample='Y'.
          'Case "parent_sample_code"
           ' ERR01(e)        'Parent_sample_code is required if rt_sample_type.needs_parent_sample='Y'.
           ' ERR03(e)        'Parent_Sample_Code must have parent record.
        'End Select
	End Sub
    	Public Sub FieldSampleKey_ColumnChanged(ByVal sender As Object, ByVal e As System.Data.DataColumnChangeEventArgs)
		'this function is called whenever the user changes a value,
		'so you can check for custom errors, etc.
	Select Case e.Column.ColumnName.ToLower
      
      Case "sample_type_code"
        ERR01(e)        'Parent_sample_code is required if rt_sample_type.needs_parent_sample='Y'.
      Case "parent_sample_code"
        ERR01(e)        'Parent_sample_code is required if rt_sample_type.needs_parent_sample='Y'.
        ERR03(e)        'Parent_Sample_Code must have parent record.
      Case "start_depth"
        ERR04(e)   'start_depth required is matrix_code = 'SO' or 'SE'
        ERR05(e)   'If start_depth is not null, end_depth must be greater than start_depth.'  
      Case "end_depth"
        ERR05(e)   'If start_depth is not null, end_depth must be greater than start_depth.'  
    End Select

	End Sub

#Region "Check"
  ' <summary>Parent_sample_code is required if rt_sample_type.needs_parent_sample='Y'.
  Friend Sub ERR01(ByVal e As System.Data.DataColumnChangeEventArgs)
    With e.Row
      If .IsNull("parent_sample_code") AndAlso Not .IsNull("sample_type_code") Then
        Dim row As System.Data.DataRow = Me._efd.package.Tables("rt_sample_type").Rows.Find(.Item("sample_type_code"))
        If row IsNot Nothing AndAlso row.Item("needs_parent_sample").ToString.ToUpper = "Y" Then
          Me.AddError(e.Row, e.Row.Table.Columns.Item("parent_sample_code"), EddErrors.CustomError1)
        Else
          Me.RemoveError(e.Row, e.Row.Table.Columns.Item("parent_sample_code"), EddErrors.CustomError1)
        End If

      Else
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("parent_sample_code"), EddErrors.CustomError1)
      End If
    End With
  End Sub

  ''' <summary>Parent_Sample_Code must have parent record.
  Friend Sub ERR03(ByVal e As System.Data.DataColumnChangeEventArgs)
    If IsCommentRow(e.Row) Then Return
    With e.Row
      If Not .IsNull("Parent_Sample_Code") Then
        Dim rows() As System.Data.DataRow = e.Row.Table.Select(String.Format("sys_sample_code='{0}'", .Item("Parent_Sample_Code").ToString.Replace("'", "''")))
        If rows.Length > 0 Then
          Me.RemoveError(e.Row, CType(.Table.Columns.Item("Parent_Sample_Code"), System.Data.DataColumn), EddErrors.OrphanRow)
        Else
          Me.AddError(e.Row, CType(.Table.Columns.Item("Parent_Sample_Code"), System.Data.DataColumn), EddErrors.OrphanRow)
        End If
      Else
        Me.RemoveError(e.Row, CType(.Table.Columns.Item("Parent_Sample_Code"), System.Data.DataColumn), EddErrors.OrphanRow)
      End If
    End With
  End Sub
  'ERR04: Start_depth cannot be null when sample_matrix_code=SO or SE. (12)
  Friend Sub ERR04(ByVal e As System.Data.DataColumnChangeEventArgs)
    With e.Row
      'KP_Case_4492_20040909:'passing the value to a function to be converted to uppercase
      If (Utilities.String.ToUpper(.Item("matrix_code")) = "SO" Or Utilities.String.ToUpper(.Item("matrix_code")) = "SE") And .Item("start_depth") Is DBNull.Value Then
        Me.AddError(e.Row, e.Row.Table.Columns.Item("start_depth"), EddErrors.CustomError4)
      Else
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("start_depth"), EddErrors.CustomError4)
      End If
    End With
  End Sub
'ERR05: If start_depth is not null, end_depth must be greater than start_depth. (04)
  Friend Sub ERR05(ByVal e As System.Data.DataColumnChangeEventArgs)
    Dim start_depth, end_depth As Double
    Dim provider As System.IFormatProvider = Nothing

    With e.Row
      ' convert the end_depth and start_depth to double
      If Not (.Item("end_depth") Is DBNull.Value) Then Double.TryParse(.Item("end_depth"), Globalization.NumberStyles.Any, provider, end_depth)
      If (Not .Item("start_depth") Is DBNull.Value) Then Double.TryParse(.Item("start_depth"), Globalization.NumberStyles.Any, provider, start_depth)

      If (((.Item("end_depth") Is DBNull.Value) AndAlso (Not .Item("start_depth") Is DBNull.Value)) _
      OrElse ((Not .Item("start_depth") Is DBNull.Value) AndAlso (Not .Item("end_depth") Is DBNull.Value) _
      AndAlso (end_depth < start_depth))) Then
        Me.AddError(e.Row, .Table.Columns.Item("end_depth"), EddErrors.CustomError5)
      Else
        Me.RemoveError(e.Row, .Table.Columns.Item("end_depth"), EddErrors.CustomError5)
      End If
    End With

  End Sub

  Private Function IsCommentRow(ByVal row As System.Data.DataRow) As Boolean
    For Each symbol As String In _efd.CommentIndicator
      If row.Item(0).ToString.StartsWith(symbol) Then Return True
    Next

    Return False
  End Function
#End Region

#Region "Create"
  'Public Shadows Function GetTestID(ByVal eddRow As System.Data.DataRow, ByVal targetRow As System.Data.DataRow) As Integer
  '  Dim dv As System.Data.DataView
  '  Static testId As Integer

  '  ' cache the last test that was accessed
  '  Static lastKey As TestAlternateKey
  '  Dim thisKey As TestAlternateKey

  '  ' get all of the values for this key
  '  With thisKey
  '    .sample_id = GetSampleId(eddRow, targetRow)
  '    .analytic_method = eddRow.Item("Lab_Anl_Method_Name").ToString
  '    .analysis_date = GetAnalysisDate(eddRow)
  '    .test_type = eddRow.Item("Test_Type").ToString
  '    .fraction = eddRow.Item("Fraction").ToString
  '  End With

  '  ' does the last key we found match this key?
  '  If Not lastKey.Equals(thisKey) Then

  '    ' cache this key
  '    lastKey = thisKey

  '    ' build the filter
  '    Dim filter As New System.Text.StringBuilder(255)
  '    filter.Append("sample_id = '" & thisKey.sample_id & "' AND analytic_method = '" & thisKey.analytic_method.Replace("'", "''") & "' ")
  '    filter.Append(String.Format(" and isnull(analysis_date, #0001-01-01 00:00:00#) = #{0}#", thisKey.analysis_date.ToString("yyyy-MM-dd HH:mm:ss")))
  '    filter.Append(String.Format(" and isnull(fraction, '') = '{0}'", thisKey.fraction.Replace("'", "''")))
  '    filter.Append(String.Format(" and isnull(test_type, '') = '{0}'", thisKey.test_type.Replace("'", "''")))

  '    ' get the test_id from dt_test
  '    dv = targetRow.Table.DataSet.Tables("dt_test").DefaultView
  '    dv.RowFilter = filter.ToString
  '    If dv.Count > 0 Then
  '      'VJN_Case_6526_20050521 'changed DirectCast to CType since DirectCast was giving invalid cast exception
  '      testId = CType(dv.Item(0).Item("test_id"), Integer)
  '    Else
  '      testId = -1
  '    End If

  '  End If

  '  Return testId
  'End Function

  Public Function Populate_dt_test_batch(ByVal eddRow As System.Data.DataRow) As Boolean
    Return Not eddRow.IsNull("Test_Batch_ID")
  End Function

  Public Function Populate_dt_location(ByVal eddRow As System.Data.DataRow) As Boolean
    Return Not eddRow.IsNull("Sys_Loc_Code")
  End Function
#End Region

#Region "Grid Events"
	Public Overrides Sub Grid_AfterCellUpdate(ByVal sender As Object, ByVal e As Object, ByVal edp As Object)
		If sender.Name.Equals("FieldSampleKey") Then
			LookupChemicalName(sender, e)
        End If

    Select Case e.Cell.Column.Key.ToLower
      'Case "sample_date"
       ' edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("Analysis_Date"))
      Case "sample_type_code"
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("parent_sample_code"))
    End Select

  End Sub

	''' This routine may be overriden to provide custom handling when a cell drop-down list closes up.
	Public Sub LookupChemicalName(ByVal sender As Object, ByVal e As Object)

		' to get access to the selected row (of the drop-down), use:
		' CType(e.Cell.Column.ValueList, Infragistics.Win.UltraWinGrid.UltraDropDown).SelectedRow

		'when they select a cas_number, populate the param_name      
    If e.Cell.Column.Key = "Cas_Rn" AndAlso Not e.Cell.Column.ValueList.SelectedRow Is Nothing Then
      Dim row As Object = e.Cell.Column.ValueList.SelectedRow
      e.Cell.Row.Cells.Item("Chemical_Name").Value = row.Cells.Item("chemical_name").Value
    End If

	End Sub
#End Region

End Class
''' <summary>
'''   $Header: /EarthSoft_2.0/EDP/Formats/EFWEDD/EFWEDD.vb   39   2008-02-29 16:40:22-07:00   mweaver $
'''		$UTCDate: 2008-02-29 23:40:22Z $
''' </summary>
Option Strict Off

Imports EarthSoft.Common
Imports EarthSoft.Edp
Imports System
Imports System.Collections

''' <summary>
'''     This is the parent class that will handle the EFWEDD (4-File) format
''' </summary>
'''
Public Class EFWEDDHandler
  Inherits EarthSoft.EDP.EddCustomHandler
  Private _OpenDialog As Object
  Private pkg As EddPackage

  ''' <summary>We use these tables for several checks, so we'll just keep references.</summary>
  Private EFW2FSample As EarthSoft.EDP.EddTable
  Private EFW2LabSMP As EarthSoft.EDP.EddTable
  Private EFW2LabRES As EarthSoft.EDP.EddTable
  Private EFW2LabTST As EarthSoft.EDP.EddTable

  ''' <summary>This are used for checking for child rows</summary>
  Private Smp_Res As EarthSoft.EDP.EddRelation
  Private FSample_Tst As EarthSoft.EDP.EddRelation
  Private LabSMP_Tst As EarthSoft.EDP.EddRelation

  ''' <summary>Several checks lookup the sample for a given test.  Instead of looking it up over, and over,
  ''' we will cache each lookup in this variable so we don't have to look it up again.</summary>
  Private sampleRow As System.Data.DataRow

  Private sampleTypeCode_for_ERR21 As New System.Collections.Specialized.StringCollection
  Private sampleTypeCode_for_ERR39 As New System.Collections.Specialized.StringCollection
  Private sampleTypeCode_for_DQM21 As New System.Collections.Specialized.StringCollection
  Private customError10Message As String
  ' common checks
  Private resultChecker As EarthSoft.EDP.Checks.Result

  Public Overrides Property Err() As EddError
    Get
      Return MyBase.Err
    End Get
    Set(ByVal Value As EddError)
      Me._Err = Value
      'TODO: set the Warning status where applicable
    End Set
  End Property

  Public Sub New()
    ' call the base class constructor
    MyBase.New()

    'VJN_Case_5282_20050525 Commented and now getting the sample type codes from rt_sample_type.
    'create stringcollection object for ERR21, so it doesn't have to be created for each iteration
    'sampleTypeCode_for_ERR21.Add("BD")
    'sampleTypeCode_for_ERR21.Add("FD")
    'sampleTypeCode_for_ERR21.Add("FR")
    'sampleTypeCode_for_ERR21.Add("FS")
    'sampleTypeCode_for_ERR21.Add("LR")
    'sampleTypeCode_for_ERR21.Add("MS")
    'sampleTypeCode_for_ERR21.Add("SD")
    'sampleTypeCode_for_ERR21.Add("MSD")

    'create stringcollection object for ERR24 and ERR39
    sampleTypeCode_for_ERR39.Add("TB")
    sampleTypeCode_for_ERR39.Add("N")
    sampleTypeCode_for_ERR39.Add("MB")
    sampleTypeCode_for_ERR39.Add("FD")
    sampleTypeCode_for_ERR39.Add("FB")
    sampleTypeCode_for_ERR39.Add("EB")
    sampleTypeCode_for_ERR39.Add("AB")

    'create stringcollection object for DQM01
    sampleTypeCode_for_DQM21.Add("N")
    sampleTypeCode_for_DQM21.Add("FD")
    sampleTypeCode_for_DQM21.Add("FR")
    sampleTypeCode_for_DQM21.Add("FS")
    sampleTypeCode_for_DQM21.Add("LR")
    sampleTypeCode_for_DQM21.Add("MS")
    sampleTypeCode_for_DQM21.Add("SD")
    sampleTypeCode_for_DQM21.Add("MSD")

    ' create the object to do the common result checks
    Me.resultChecker = New EarthSoft.EDP.Checks.Result(Me)

  End Sub

  Public Overrides Sub AddDataHandlers(ByRef Efd As EarthSoft.EDP.EddFormatDefinition)

    Me.EFW2LabRES = Efd.Tables.Item("EFW2LabRES")
    Me.EFW2FSample = Efd.Tables.Item("EFW2FSample")
    Me.EFW2LabSMP = Efd.Tables.Item("EFW2LabSMP")
    Me.EFW2LabTST = Efd.Tables.Item("EFW2LabTST")
    'KP_Case9239_20060327
    Me.pkg = Efd.package

    AddHandler Me.EFW2LabRES.ColumnChanged, AddressOf Me.Check_EFW2LabRES
    AddHandler Me.EFW2LabRES.BeforeDataLoad, AddressOf Me.BeforeDataLoad

    AddHandler Me.EFW2FSample.ColumnChanged, AddressOf Me.Check_EFW2FSample
    AddHandler Me.EFW2FSample.BeforeDataLoad, AddressOf Me.BeforeDataLoad

    AddHandler Me.EFW2LabSMP.ColumnChanged, AddressOf Me.Check_EFW2LabSMP
    AddHandler Me.EFW2LabSMP.BeforeDataLoad, AddressOf Me.BeforeDataLoad

    AddHandler Me.EFW2LabTST.ColumnChanged, AddressOf Me.Check_EFW2LabTST
    AddHandler Me.EFW2LabTST.BeforeDataLoad, AddressOf Me.BeforeDataLoad

    'ERR20 will listen to RowChanged of EFW2LabSMP so it can check for child rows
    AddHandler Me.EFW2LabSMP.RowChanged, AddressOf Me.ERR20

    ' get the relations to find child rows
    Me.Smp_Res = CType(EFW2LabRES, EddTable).ParentRelations.Item("FK_EFW2LabRES_EFW2LabSMP")
    Me.FSample_Tst = CType(EFW2LabTST, EddTable).ParentRelations.Item("FK_EFW2LabTST_EFW2FSample")
    Me.LabSMP_Tst = CType(EFW2LabTST, EddTable).ParentRelations.Item("FK_EFW2LabTST_EFW2LabSMP")

    If Efd.package.Tables.Contains("rt_sample_type") AndAlso Efd.package.Tables("rt_sample_type").Columns.Contains("needs_parent_sample") Then
      'VJN_Case_5282_20050525 Get the sample types that requires parent sample code.
      For Each dr As System.Data.DataRow In Efd.package.Tables("rt_sample_type").Select("needs_parent_sample='Y'")
        sampleTypeCode_for_ERR21.Add(Utilities.String.ToUpper(dr.Item("sample_type_code")))
        customError10Message = customError10Message & dr.Item("sample_type_code").ToString & ","
      Next
      'remove trailing comma
      customError10Message = customError10Message.Substring(0, customError10Message.Length - 1)
    End If
  End Sub

  'FB.11291: we need to clear the member variables before reloading data
  Private Sub BeforeDataLoad(ByVal eddTable As EarthSoft.EDP.EddTable)
    Me.sampleRow = Nothing
  End Sub

  Public Overloads Overrides Function ErrorMessage(ByVal err As EddErrors) As String

    Select Case err
      Case EddErrors.CustomError1
        Return "Percent_moisture cannot be null when sample matrix = SO or SE and sample type = N, FD, FR, FS, LR, MS, SD or MSD. (1)"
      Case EddErrors.CustomError2
        Return "Reportable_result cannot be 'Yes' where lab_qualifiers=E, G, P, or R. (2)"
      Case EddErrors.CustomError3
        Return "Datum_unit cannot be null if datum_value is not null. (3)"
      Case EddErrors.CustomError4
        Return "Sample must have related test/result. (20)"
      Case EddErrors.CustomError5
        Return "Reporting_detection_limit cannot be null when detect_flag=N. (5)"
      Case EddErrors.CustomError6
        Return "Result_value is required where detect_flag=Y and result_type_code=TRG, TIC. (6)"
      Case EddErrors.CustomError7
        Return "Cannot be less than the original concentration. (7)"
      Case EddErrors.CustomError8
        Return "Subsample_amount and subsample_amount_unit cannot be null when sample type = N, FD, FR, FS, LR, MS, SD or MSD. (8)"
      Case EddErrors.CustomError9
        Return "Date cannot precede sample_date. (9)"
      Case EddErrors.CustomError10
        Return "Parent_sample_code is required where sample_type_code= " & customError10Message & " (10)"
      Case EddErrors.CustomError11
        Return "Sample_date cannot be null when when sample_type_code=TB, N, MB, FD, FB, EB, AB. (11)"
      Case EddErrors.CustomError12
        Return "Start_depth cannot be null when sample_matrix_code=SO or SE. (12)"
      Case EddErrors.CustomError13
        Return "If start_depth is not null, end_depth must be greater than start_depth. (13)"
      Case EddErrors.CustomError14
        Return "Lab_name_code cannot be null when analysis_location=LB or FL. (14)"
      Case EddErrors.CustomError15
        Return "Lab_sample_id cannot be null when analysis_location=LB. (15)"
      Case EddErrors.CustomError16
        Return "Result_unit cannot be null when result_value is not null. (16)"
      Case EddErrors.CustomError17
        Return "Detection_limit_unit cannot be null when reporting_detection_limit is not null. (17)"
      Case EddErrors.CustomError18
        Return "Depth_unit cannot be null if start_depth is not null. (18)"
      Case EddErrors.CustomError19
        Return "Sample_time cannot be null when sample_type_code=TB, N, MB, FD, FB, EB, AB. (19)"
      Case EddErrors.CustomError20
        Return "If analysis_location=LB, sample_delivery_group, sent_to_lab_date, and sample_receipt_date cannot be null. (20)"
    End Select

    Return String.Empty
  End Function

  Private Sub Check_EFW2LabRES(ByVal sender As Object, ByVal e As System.Data.DataColumnChangeEventArgs)

    Select Case e.Column.ColumnName.ToLower
      Case "analysis_date"
        ERR08_09(e, e.Column.ColumnName, Me.Smp_Res)        'ERR08: Analysis_date cannot precede sample_date. (9)
        'KP_Case7617_20051031

        ' the following lines were commented out by jcm 12012005 until the logic can be defined more clearly
        ' Case "qc_spike_measured" -->
        'Me.resultChecker.qc_spike_measured_NotLessThan_qc_spike_added(e) 			 'qc_spike_measured cannot be less than qc_spike_added
        'KP_Case7617_20051031
        ' Case "qc_spike_added"
        'Me.resultChecker.qc_spike_measured_NotLessThan_qc_spike_added(e) 				'qc_spike_measured cannot be less than qc_spike_added
        'Case "qc_dup_spike_measured"
        'Me.resultChecker.qc_dup_spike_measured_NotLessThan_qc_dup_original_conc(e)				 'ERR17: qc_dup_spike_measured cannot be less than qc_dup_original_conc
        'Case "qc_dup_original_conc"
        'Me.resultChecker.qc_dup_spike_measured_NotLessThan_qc_dup_original_conc(e)				 'ERR17: qc_dup_spike_measured cannot be less than qc_dup_original_conc

      Case "result_value"
        ERR22(e)         'ERR22: Result_value is required where detect_flag='Y' (6)
      Case "detect_flag"
        ERR22(e)         'ERR22: Result_value is required where detect_flag='Y' (6)
        ERR23(e)         'ERR23: Reporting_detection_limit cannot be null when detect_flag=N. (5)
      Case "reporting_detection_limit"
        ERR23(e)         'ERR23: Reporting_detection_limit cannot be null when detect_flag=N. (5)
        ERR30(e)         'ERR30: Detection_limit_unit cannot be null when reporting_detection_limit is not null. (17)
      Case "detection_limit_unit"
        ERR30(e)         'ERR30: Detection_limit_unit cannot be null when reporting_detection_limit is not null. (17)

        'FB.8555: do NOT check lab_qualifiers
        'Case "lab_qualifiers"
        '	resultChecker.VerifyQualifiers(e)
    End Select

  End Sub

  Private Sub Check_EFW2FSample(ByVal sender As Object, ByVal e As System.Data.DataColumnChangeEventArgs)

    Select Case e.Column.ColumnName.ToLower
      Case "sample_type_code"
        ERR21(e)         'ERR21: Parent_sample_code is required where sample_type_code=BD, FD, FR, FS, LR, MS, SD, or MSD. (10)
        ERR24(e)         'ERR24: sample_date cannot be null when when sample_type_code=TB, N, MB, FD, FB, EB, AB. (11)

        ' 20071031-mjw: we cannot call these functions here because the flag the error in LabTST (not FSample)
        'DQM01(e, Me.FSample_Tst)        'DQM01: Percent_Moisture is required where sample_matrix_code=SO or SE and sample_type_code is an N, 'FD', 'FR', 'FS', 'LR', 'MS', 'SD',' or MSD' (10)
        'DQM02(e, Me.FSample_Tst)        'DQM02: Subsample_amount is required where sample_type_code is an N, 'FD', 'FR', 'FS', 'LR', 'MS', 'SD',' or MSD' (10)
      Case "sample_matrix_code"
        'DQM01(e, Me.FSample_Tst)        'DQM01: Percent_Moisture is required where sample_matrix_code=SO or SE and sample_type_code is an N, 'FD', 'FR', 'FS', 'LR', 'MS', 'SD',' or MSD' (10)
      Case "parent_sample_code"
        ERR21(e)         'ERR21: Parent_sample_code is required where sample_type_code=BD, FD, FR, FS, LR, MS, SD, or MSD. (10)
      Case "sample_date"
        ERR24(e)         'ERR24: sample_date cannot be null when when sample_type_code=TB, N, MB, FD, FB, EB, AB. (11)
      Case "start_depth"
        ERR25(e)         'ERR25: Start_depth cannot be null when sample_matrix_code=SO or SE. (12)
        ERR26(e)         'ERR26: If start_depth is not null, end_depth must be greater than start_depth. (13)
      Case "end_depth"
        ERR26(e)         'ERR26: If start_depth is not null, end_depth must be greater than start_depth. (13)
    End Select

  End Sub

  Private Sub Check_EFW2LabSMP(ByVal sender As Object, ByVal e As System.Data.DataColumnChangeEventArgs)
    Select Case e.Column.ColumnName.ToLower
      Case "parent_sample_code"
        ERR21(e)         'ERR21: Parent_sample_code is required where sample_type_code=BD, FD, FR, FS, LR, MS, SD, or MSD. (10)
      Case "sample_type_code"
        ERR21(e)         'ERR21: Parent_sample_code is required where sample_type_code=BD, FD, FR, FS, LR, MS, SD, or MSD. (10)
        ERR24(e)                 'ERR24: sample_date cannot be null when when sample_type_code=TB, N, MB, FD, FB, EB, AB. (11)
      Case "sample_date"
        ERR24(e)                 'ERR24: sample_date cannot be null when when sample_type_code=TB, N, MB, FD, FB, EB, AB. (11)
    End Select
  End Sub

  Private Sub Check_EFW2LabTST(ByVal sender As Object, ByVal e As System.Data.DataColumnChangeEventArgs)

    Select Case e.Column.ColumnName.ToLower
      Case "prep_date"
        ERR08_09(e, e.Column.ColumnName, Me.FSample_Tst)         'ERR09: prep_date cannot precede sample_date. (9)
      Case "percent_moisture"
        DQM01(e, Me.FSample_Tst)        'DQM01: Percent_Moisture is required where sample_matrix_code=SO or SE and sample_type_code is an N, 'FD', 'FR', 'FS', 'LR', 'MS', 'SD',' or MSD' (10)
        DQM01(e, Me.LabSMP_Tst)
      Case "subsample_amount"
        DQM02(e, Me.FSample_Tst)        'DQM02: Subsample_amount is required where sample_type_code is an N, 'FD', 'FR', 'FS', 'LR', 'MS', 'SD',' or MSD' (10)
        DQM02(e, Me.LabSMP_Tst)
        DQM03(e)         'DQM03: Subsample_amount_unit cannot be null when Subsample_amount is not null. (16)
      Case "subsample_amount_unit"
        DQM03(e)         'DQM03: Subsample_amount_unit cannot be null when Subsample_amount is not null. (16)
      Case "lab_name_code"
        'The element lab_name_code is a required field in EFW2LabTST  format . So the Checks ERR27 can be skipped
        'ERR27(e)            'ERR27: Lab_name_code cannot be null when analysis_location=LB or FL. (14)
      Case "analysis_location"
        'The element lab_name_code is a required field in EFW2LabTST  format . So the Checks ERR27 can be skipped
        'ERR27(e)            'ERR27: Lab_name_code cannot be null when analysis_location=LB or FL. (14)
        ERR28(e)         'ERR28: Lab_sample_id cannot be null when analysis_location=LB. (15)
      Case "lab_sample_id"
        ERR28(e)         'ERR28: Lab_sample_id cannot be null when analysis_location=LB. (15)
    End Select
  End Sub
  'KP_Case9239_20060323
  Public Function CreateFieldSample(ByVal eddRow As System.Data.DataRow) As Boolean
    'KP_Case9239_20060327
    Dim row() As System.Data.DataRow = Me.pkg.Tables.Item("rt_sample_type").Select("sample_type_code = '" & eddRow.Item("sample_type_code") & "'")

    If row(0).Item("sample_type_class") = "FQ" Or row(0).Item("sample_type_class") = "NF" Then
      Return True
    Else
      Return False
    End If
  End Function
  Public Function CreateLabSample(ByVal eddRow As System.Data.DataRow) As Boolean
    Dim row() As System.Data.DataRow = Me.pkg.Tables.Item("rt_sample_type").Select("sample_type_code = '" & eddRow.Item("sample_type_code") & "'")

    If row(0).Item("sample_type_class") = "LQ" Then
      Return True
    Else
      Return False
    End If
  End Function

#Region "CustomChecks"

  'ERR08: Analysis_date cannot precede sample_date. (9)
  'ERR09: prep_date cannot precede sample_date. (9)
  Friend Sub ERR08_09(ByVal e As System.Data.DataColumnChangeEventArgs, ByVal date_field As String, ByVal relation As EarthSoft.EDP.EddRelation)

    ' if for some reason we don't have sample rows, just exit
    If (relation Is Me.Smp_Res) AndAlso (EFW2LabSMP.Rows.Count <= 0) Then
      Return
    ElseIf (relation Is Me.FSample_Tst) AndAlso (EFW2FSample.Rows.Count <= 0) Then
      Return
    End If

    With e.Row
      Try
        ' do we need to lookup the sample row?
        If (Me.sampleRow Is Nothing) OrElse (Not Me.sampleRow.Item("sys_sample_code").ToString.Equals(.Item("sys_sample_code"))) Then
          ' use the relation to get the parent row for this sample
          Me.sampleRow = relation.GetParentRow(e.Row)
          ' make sure it found the row
          If Me.sampleRow Is Nothing Then Return
        End If

        ' make sure both dates are non-null then compare
        If (Not .Item(date_field) Is DBNull.Value) AndAlso (Not Me.sampleRow.Item("sample_date") Is DBNull.Value) AndAlso _
         (System.DateTime.Compare(CType(.Item(date_field), Date), CType(Me.sampleRow.Item("sample_date"), Date)) < 0) Then
          Me.AddError(e.Row, DirectCast(.Table.Columns.Item(date_field), System.Data.DataColumn), EarthSoft.EDP.EddErrors.CustomError9)
        Else
          Me.RemoveError(e.Row, .Table.Columns.Item(date_field), EarthSoft.EDP.EddErrors.CustomError9)
        End If

      Catch ex As Exception
        'if date conversion doesn't work, we don't want error because comparison is moot.  Just move on...
      End Try
    End With
  End Sub

  ''' <summary>This method will check the EFW2LabSMP Table to verify child rows.
  ''' Parent rows for test/results are checked by default (Because of the xs:keyref)</summary>
  Public Sub ERR20(ByVal sender As Object, ByVal e As System.Data.DataRowChangeEventArgs)
    ' NOTE: the relation names must exactly match the xs:keyref names in the *.xsd

    Try
      If Me.EFW2LabRES.Rows.Count = 0 Then
        ' if there are no records in either child table, then assume no results were loaded, so there is no error
        Me.RemoveError(e.Row, EddErrors.CustomError4)

      ElseIf Me.Smp_Res.GetChildRows(e.Row).Length > 0 Then
        ' if there is at least one child row in either table, then there is no error
        Me.RemoveError(e.Row, EddErrors.CustomError4)
      Else
        ' there are rows in at least one of the child tables, but neither table contains a matching row
        Me.AddError(e.Row, EddErrors.CustomError4)
      End If

    Catch ex As Exception
      ' EarthSoft.Shared.MsgBox.Show(ex.ToString)
    End Try

  End Sub

  'ERR21: Parent_sample_code is required where sample_type_code=BD, FD, FR, FS, LR, MS, SD, or MSD. (10)
  Friend Sub ERR21(ByVal e As System.Data.DataColumnChangeEventArgs)
    With e.Row

      'KP_Case_4492_20040909:'passing the value to a function to be converted to uppercase
      If .Item("parent_sample_code") Is DBNull.Value And sampleTypeCode_for_ERR21.Contains(Utilities.String.ToUpper(.Item("sample_type_code"))) Then
        Me.AddError(e.Row, e.Row.Table.Columns.Item("parent_sample_code"), EddErrors.CustomError10)
      Else
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("parent_sample_code"), EddErrors.CustomError10)
      End If
    End With
  End Sub

  'VJN_20041028
  'ERR22: Result_value is required where detect_flag=Y and result_type_code=TRG, TIC. (6)
  Friend Sub ERR22(ByVal e As System.Data.DataColumnChangeEventArgs)

    With e.Row
      If Not .Item("detect_flag") Is DBNull.Value AndAlso Utilities.String.ToUpper(.Item("detect_flag")) = "Y" AndAlso _
      (Utilities.String.ToUpper(.Item("result_type_code")) = "TRG" OrElse Utilities.String.ToUpper(.Item("result_type_code")) = "TIC") AndAlso _
      .Item("result_value") Is DBNull.Value Then
        Me.AddError(e.Row, e.Row.Table.Columns.Item("detect_flag"), EddErrors.CustomError6)
      Else
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("detect_flag"), EddErrors.CustomError6)
      End If
    End With

  End Sub

  'ERR23: Reporting_detection_limit cannot be null when detect_flag=N. (5)
  Friend Sub ERR23(ByVal e As System.Data.DataColumnChangeEventArgs)
    Dim rtc As String

    With e.Row
      rtc = Utilities.String.ToUpper(.Item("result_type_code"))
      If Utilities.String.ToUpper(.Item("detect_flag")) = "N" AndAlso (rtc = "TRG" OrElse rtc = "TIC" OrElse rtc = "SC") AndAlso .Item("reporting_detection_limit") Is DBNull.Value Then
        Me.AddError(e.Row, e.Row.Table.Columns.Item("detect_flag"), EddErrors.CustomError5)
      Else
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("detect_flag"), EddErrors.CustomError5)
      End If
    End With
  End Sub

  'ERR24: sample_date cannot be null when when sample_type_code=TB, N, MB, FD, FB, EB, AB. (11)
  Friend Sub ERR24(ByVal e As System.Data.DataColumnChangeEventArgs)
    With e.Row
      'KP_Case_4492_20040909:'passing the value to a function to be converted to uppercase
      If .Item("sample_date") Is DBNull.Value AndAlso sampleTypeCode_for_ERR39.Contains(Utilities.String.ToUpper(.Item("sample_type_code"))) Then
        'Me.AddError(e.Row, DirectCast(.Table.Columns.Item("sample_date"), System.Data.DataColumn), EarthSoft.Edp.EddErrors.CustomError11)
        Me.AddError(e.Row, .Table.Columns.Item("sample_date"), EddErrors.CustomError11)
      Else
        Me.RemoveError(e.Row, .Table.Columns.Item("sample_date"), EddErrors.CustomError11)
      End If
    End With
  End Sub

  'ERR25: Start_depth cannot be null when sample_matrix_code=SO or SE. (12)
  Friend Sub ERR25(ByVal e As System.Data.DataColumnChangeEventArgs)
    With e.Row
      'KP_Case_4492_20040909:'passing the value to a function to be converted to uppercase
      If (Utilities.String.ToUpper(.Item("sample_matrix_code")) = "SO" Or Utilities.String.ToUpper(.Item("sample_matrix_code")) = "SE") And .Item("start_depth") Is DBNull.Value Then
        Me.AddError(e.Row, e.Row.Table.Columns.Item("start_depth"), EddErrors.CustomError12)
      Else
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("start_depth"), EddErrors.CustomError12)
      End If
    End With
  End Sub

  'ERR26: If start_depth is not null, end_depth must be greater than start_depth. (13)
  Friend Sub ERR26(ByVal e As System.Data.DataColumnChangeEventArgs)
    Dim start_depth, end_depth As Double
    Dim provider As System.IFormatProvider = Nothing

    With e.Row
      ' Rekha_Case4562_20041015
      ' convert the end_depth and start_depth to double
      If Not (.Item("end_depth") Is DBNull.Value) Then Double.TryParse(.Item("end_depth"), Globalization.NumberStyles.Any, provider, end_depth)
      If (Not .Item("start_depth") Is DBNull.Value) Then Double.TryParse(.Item("start_depth"), Globalization.NumberStyles.Any, provider, start_depth)

      If (((.Item("end_depth") Is DBNull.Value) AndAlso (Not .Item("start_depth") Is DBNull.Value)) _
      OrElse ((Not .Item("start_depth") Is DBNull.Value) AndAlso (Not .Item("end_depth") Is DBNull.Value) _
      AndAlso (end_depth < start_depth))) Then
        Me.AddError(e.Row, .Table.Columns.Item("end_depth"), EddErrors.CustomError13)
      Else
        Me.RemoveError(e.Row, .Table.Columns.Item("end_depth"), EddErrors.CustomError13)
      End If
    End With

  End Sub

  'ERR27: Lab_name_code cannot be null when analysis_location=LB or FL. (14)
  'The element lab_name_code is a required field in EFW2LabTST  format . So the Checks ERR27 can be skipped
  'Private Sub ERR27(ByVal e As System.Data.DataColumnChangeEventArgs)
  '    With e.Row
  '        If (.Item("analysis_location").ToUpper = "LB" OrElse .Item("analysis_location").ToUpper = "FL") AndAlso .Item("lab_name_code") Is DBNull.Value Then
  '            Me.AddError(e.Row, e.Row.Table.Columns.Item("lab_name_code"), EddErrors.CustomError1)
  '        Else
  '            Me.RemoveError(e.Row, e.Row.Table.Columns.Item("lab_name_code"), EddErrors.CustomError1)
  '        End If
  '    End With
  'End Sub

  'ERR28: Lab_sample_id cannot be null when analysis_location=LB. (15)
  Friend Sub ERR28(ByVal e As System.Data.DataColumnChangeEventArgs)
    With e.Row
      'VJN_Case4562_20041019
      If (Utilities.String.ToUpper(.Item("analysis_location")) = "LB" AndAlso .Item("lab_sample_id") Is DBNull.Value) Then
        Me.AddError(e.Row, e.Row.Table.Columns.Item("lab_sample_id"), EddErrors.CustomError15)
      Else
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("lab_sample_id"), EddErrors.CustomError15)
      End If
    End With
  End Sub

  'ERR29: Result_unit cannot be null when result_value is not null. (16)
  'vin: According to the revised document this check is not needed.
  'Private Sub ERR29(ByVal e As System.Data.DataColumnChangeEventArgs)
  '    With e.Row
  '        If (Not .Item("result_value") Is DBNull.Value AndAlso .Item("result_unit") Is DBNull.Value) Then
  '            Me.AddError(e.Row, e.Row.Table.Columns.Item("result_unit"), EddErrors.CustomError1)
  '        Else
  '            Me.RemoveError(e.Row, e.Row.Table.Columns.Item("result_unit"), EddErrors.CustomError1)
  '        End If
  '    End With
  'End Sub

  'ERR30: Detection_limit_unit cannot be null when reporting_detection_limit is not null. (17)
  Friend Sub ERR30(ByVal e As System.Data.DataColumnChangeEventArgs)
    With e.Row
      If (Not .Item("reporting_detection_limit") Is DBNull.Value AndAlso .Item("detection_limit_unit") Is DBNull.Value) Then
        Me.AddError(e.Row, e.Row.Table.Columns.Item("detection_limit_unit"), EddErrors.CustomError17)
      Else
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("detection_limit_unit"), EddErrors.CustomError17)
      End If
    End With
  End Sub

  'DQM01: Percent_Moisture is required where sample_matrix_code=SO or SE and sample_type_code is an N, 'FD', 'FR', 'FS', 'LR', 'MS', 'SD',' or MSD' (10)
  ' Used fields are EFW2LabTST.Percent_Moisture,EFW2FSample.sample_type_code and EFW2FSample.sample_matrix_code
  Private Sub DQM01(ByVal e As System.Data.DataColumnChangeEventArgs, ByVal relation As EarthSoft.EDP.EddRelation)

    ' if for some reason we don't have sample rows, just exit
    If relation Is Me.FSample_Tst AndAlso Me.EFW2FSample.Rows.Count <= 0 Then Return
    If relation Is Me.LabSMP_Tst AndAlso Me.EFW2LabSMP.Rows.Count <= 0 Then Return

    With e.Row
      Try
        ' do we need to lookup the sample row?
        If (Me.sampleRow Is Nothing) OrElse (Not Me.sampleRow.Item("sys_sample_code").ToString.Equals(.Item("sys_sample_code"))) Then
          ' use the relation to get the parent row for this sample
          Me.sampleRow = relation.GetParentRow(e.Row)
          ' make sure it found the row
          If Me.sampleRow Is Nothing Then Return
        End If

        If (.Item("percent_moisture") Is DBNull.Value) AndAlso (Utilities.String.ToUpper(Me.sampleRow.Item("sample_matrix_code")) = "SE" OrElse Utilities.String.ToUpper(Me.sampleRow.Item("sample_matrix_code")) = "SO") AndAlso (sampleTypeCode_for_DQM21.Contains(Utilities.String.ToUpper(Me.sampleRow.Item("sample_type_code")))) Then
          Me.AddError(e.Row, e.Row.Table.Columns.Item("percent_moisture"), EddErrors.CustomError1)
        Else
          Me.RemoveError(e.Row, e.Row.Table.Columns.Item("percent_moisture"), EddErrors.CustomError1)
        End If
      Catch ex As Exception
        '...
        'EarthSoft.Shared.MsgBox.Show("DQM1 : " & ex.ToString)
      End Try
    End With
  End Sub

  'DQM02: Subsample_amount is required where sample_type_code is an N, 'FD', 'FR', 'FS', 'LR', 'MS', 'SD',' or MSD' (10)
  ' Used fields are EFW2LabTST.subsample_amount and EFW2FSample.sample_type_code
  Private Sub DQM02(ByVal e As System.Data.DataColumnChangeEventArgs, ByVal relation As EarthSoft.EDP.EddRelation)

    ' if for some reason we don't have sample rows, just exit
    If relation Is Me.FSample_Tst AndAlso Me.EFW2FSample.Rows.Count <= 0 Then Return
    If relation Is Me.LabSMP_Tst AndAlso Me.EFW2LabSMP.Rows.Count <= 0 Then Return

    With e.Row
      Try
        ' do we need to lookup the sample row?
        If (Me.sampleRow Is Nothing) OrElse (Not Me.sampleRow.Item("sys_sample_code").ToString.Equals(.Item("sys_sample_code"))) Then
          ' use the relation to get the parent row for this sample
          Me.sampleRow = relation.GetParentRow(e.Row)
          ' make sure it found the row
          If Me.sampleRow Is Nothing Then Return
        End If

        If (.Item("subsample_amount") Is DBNull.Value) AndAlso (sampleTypeCode_for_DQM21.Contains(Utilities.String.ToUpper(Me.sampleRow.Item("sample_type_code")))) Then
          Me.AddError(e.Row, e.Row.Table.Columns.Item("subsample_amount"), EddErrors.CustomError8)
        Else
          Me.RemoveError(e.Row, e.Row.Table.Columns.Item("subsample_amount"), EddErrors.CustomError8)
        End If
      Catch ex As Exception
        '...
      End Try
    End With
  End Sub

  Private Sub DQM03(ByVal e As System.Data.DataColumnChangeEventArgs)
    With e.Row
      If (Not .Item("subsample_amount") Is DBNull.Value AndAlso .Item("subsample_amount_unit") Is DBNull.Value) Then
        Me.AddError(e.Row, e.Row.Table.Columns.Item("subsample_amount_unit"), EddErrors.CustomError8)
      Else
        Me.RemoveError(e.Row, e.Row.Table.Columns.Item("subsample_amount_unit"), EddErrors.CustomError8)
      End If
    End With
  End Sub


#End Region

#Region "Grid Events"
  ''' This routine may be overriden to provide custom handling when a cell drop-down list closes up.
  Public Overrides Sub Grid_AfterCellListCloseUp(ByVal sender As Object, ByVal e As Object, ByVal edp As Object)

    ' to get access to the selected row (of the drop-down), use:
    ' CType(e.Cell.Column.ValueList, Infragistics.Win.UltraWinGrid.UltraDropDown).SelectedRow

    'NOTE: do we need to check to see if this is the right grid?

    'when they select a cas_number, populate the param_name
    If e.Cell.Column.Key = "cas_rn" Then

      ' Infragistics.Win.UltraWinGrid.UltraGridRow
      Dim row As Object

      ' CType(e.Cell.Column.ValueList, Infragistics.Win.UltraWinGrid.UltraDropDown).SelectedRow
      'VJN added : 20043004
      'Assigning value to the cell only if any row selected from the dropdown list
      If Not e.Cell.Column.ValueList.SelectedRow Is Nothing Then
        row = e.Cell.Column.ValueList.SelectedRow
        e.Cell.Row.Cells.Item("chemical_name").Value = row.Cells.Item("chemical_name").Value
      End If
    End If

  End Sub

  'for most checks, if one cell is updated, the other needs to be explicitly updated
  'because the error will need to be added/removed to both columns
  Public Overrides Sub Grid_AfterCellUpdate(ByVal sender As Object, ByVal e As Object, ByVal edp As Object)

    ' make an explicit call to AfterCellUpdate to show/clear the error on the other cell
    Select Case e.Cell.Column.Key.ToLower
      Case "qc_original_conc"
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("qc_spike_measured"))
      Case "qc_dup_original_conc"
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("qc_dup_spike_measured"))
      Case "sample_type_code"
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("sample_date"))
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("parent_sample_code"))

        If (e.Cell.Row.Band.Key.ToUpper = "EFW2FSAMPLE") Then edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("sys_loc_code"))
        If (e.Cell.Row.Band.Key.ToUpper = "EFW2LABTST") Then edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("subsample_amount"))
      Case "sample_date"
        'e.Cell.Band.Layout.Bands("EFW2LabSMP").Layout.Grid.Refresh()
      Case "result_value"
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("detect_flag"))
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("result_unit"))
      Case "reporting_detection_limit"
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("detect_flag"))
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("detection_limit_unit"))
      Case "start_depth"
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("end_depth"))
      Case "analysis_location"
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("lab_name_code"))
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("lab_sample_id"))
      Case "sample_matrix_code"
        If (e.Cell.Row.Band.Key.ToUpper = "EFW2FSAMPLE") Then edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("start_depth"))
        If (e.Cell.Row.Band.Key.ToUpper = "EFW2LABTST") Then edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("percent_moisture"))
      Case "subsample_amount"
        edp.AfterCellUpdate(sender, e.Cell.Row.Cells.Item("subsample_amount_unit"))
    End Select
  End Sub
#End Region

#Region "Open"

  Public Overloads Overrides Sub SetupOpenFileDialog(ByVal dialog As Object, ByVal FormatName As String)
    dialog.title = String.Format("Select {0} Data File", FormatName)
    dialog.multiselect = False
    Me._OpenDialog = dialog
  End Sub

#End Region

End Class


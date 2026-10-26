// Included inside [Code]. Bare MSI-style property alias for one Inno task only.
// Keep this separate from the PATH registry/ownership implementation.
var
  EvergreenPathAliasPresent, EvergreenPathAliasEnabled, EvergreenPathAliasApplied: Boolean;

function InitializeEvergreenPathTaskAlias: Boolean;
var
  I, EqualsPos: Integer;
  Argument, Name, Value, Error: String;
  Enabled: Boolean;
begin
  Result := True;
  EvergreenPathAliasPresent := False;
  EvergreenPathAliasApplied := False;
  for I := 1 to ParamCount do
  begin
    Argument := ParamStr(I);
    EqualsPos := Pos('=', Argument);
    if EqualsPos = 0 then Name := Argument
    else Name := Copy(Argument, 1, EqualsPos - 1);
    // Deliberately do not recognize /ADDTOPATH or any additional switch spelling.
    if CompareText(Trim(Name), 'ADDTOPATH') <> 0 then Continue;
    Value := Copy(Argument, EqualsPos + 1, MaxInt);
    Error := '';
    if (Name <> Trim(Name)) or (EqualsPos = 0) or ((Value <> '0') and (Value <> '1')) then
      Error := 'Invalid argument "' + Argument + '"; expected ADDTOPATH=0 or ADDTOPATH=1.'
    else
    begin
      Enabled := Value = '1';
      if EvergreenPathAliasPresent and (Enabled <> EvergreenPathAliasEnabled) then
        Error := 'Conflicting ADDTOPATH properties: both 0 and 1 were specified.';
    end;
    if Error <> '' then
    begin
      Log('ERROR: ' + Error);
      SuppressibleMsgBox(FmtMessage(CustomMessage('InvalidAddToPathProperty'), [Error]),
        mbError, MB_OK, IDOK);
      Result := False;
      Exit;
    end;
    EvergreenPathAliasPresent := True;
    EvergreenPathAliasEnabled := Enabled;
  end;
end;

procedure ApplyEvergreenPathTaskAlias;
begin
  if not EvergreenPathAliasPresent or EvergreenPathAliasApplied then Exit;
  EvergreenPathAliasApplied := True;
  // Never let selecting a child implicitly select its regular-install parent.
  if not WizardIsTaskSelected('regularinstall') then
  begin
    Log('ADDTOPATH ignored for portable installation; install type and PATH are unchanged.');
    Exit;
  end;
  // The wizard has already restored previous tasks and processed /TASKS and
  // /MERGETASKS. Only change this checkbox, once; subsequent UI edits win.
  if EvergreenPathAliasEnabled then
    WizardSelectTasks('regularinstall\addtopath')
  else
    WizardSelectTasks('!regularinstall\addtopath');
  Log('Applied ADDTOPATH=' + IntToStr(Ord(EvergreenPathAliasEnabled)) + ' to the evergreen PATH checkbox.');
end;

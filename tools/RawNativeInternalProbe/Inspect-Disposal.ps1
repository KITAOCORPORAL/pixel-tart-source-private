param([Parameter(Mandatory)][string]$Binding,[Parameter(Mandatory)][string]$Output)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Output){throw 'Preserve previous IL evidence'}
$a=[Reflection.Assembly]::LoadFile((Resolve-Path -LiteralPath $Binding).Path);$t=$a.GetType('Sdcb.LibRaw.RawContext',$true)
$method=$t.GetMethod('Dispose',[Reflection.BindingFlags]'NonPublic,Instance',$null,[Type[]]@([bool]),$null)
$op=@{};[Reflection.Emit.OpCodes].GetFields([Reflection.BindingFlags]'Public,Static')|ForEach-Object {$o=$_.GetValue($null);$op[[int]$o.Value -band 65535]=$o}
$b=$method.GetMethodBody().GetILAsByteArray();$items=@();$p=0
while($p -lt $b.Length){$at=$p;$v=[int]$b[$p++];if($v -eq 254){$v=65280+[int]$b[$p++]};$o=$op[$v];if(!$o){throw 'Unknown IL opcode'};$n=switch($o.OperandType.ToString()){'InlineNone'{0};'ShortInlineBrTarget'{1};'ShortInlineI'{1};'ShortInlineVar'{1};'InlineVar'{2};'InlineI8'{8};'InlineR'{8};'InlineSwitch'{throw 'Unexpected IL switch'};default{4}};$operand='';if($n -eq 4){$token=[BitConverter]::ToInt32($b,$p);$operand=switch($o.OperandType.ToString()){'InlineMethod'{$method.Module.ResolveMethod($token).ToString()};'InlineField'{$method.Module.ResolveField($token).ToString()};default{$token.ToString()}}};$p+=$n;$items+=@{offset=$at;opcode=$o.Name;operand=$operand}}
@{binding=(Resolve-Path $Binding).Path;sha256=(Get-FileHash -LiteralPath $Binding).Hash;method=$method.ToString();ilHex=[Convert]::ToHexString($b);instructions=$items;observation='Metadata IL only; no native calls or type initialization. Recycle/Close entry counts are inferred from Dispose logic unless separately instrumented.';nativeInit=0;rawCalls=0}|ConvertTo-Json -Depth 7|Set-Content -LiteralPath $Output

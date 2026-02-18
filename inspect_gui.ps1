$dll = [System.Reflection.Assembly]::LoadFrom('C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed\assembly_valheim.dll')
$type = $dll.GetType('InventoryGui')
if ($type) {
    $fields = $type.GetFields([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Instance)
    foreach ($f in $fields) {
        if ($f.Name -match 'craft|recipe|tab|panel|upgrade|repair|item|desc|icon|name|button|list|quality|variant|split|info') {
            Write-Output ('{0} : {1}' -f $f.Name, $f.FieldType.Name)
        }
    }
} else {
    Write-Output 'InventoryGui type not found'
}

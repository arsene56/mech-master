param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

$ErrorActionPreference = 'Stop'
$bomPath = Join-Path $RepositoryRoot 'Assets\StreamingAssets\MechanicalCatalog\bicycle_engineering.json'
$modelPath = Join-Path $RepositoryRoot 'Assets\StreamingAssets\MechanicalCatalog\bicycle_model_manifest.json'
$outputPath = Join-Path $RepositoryRoot 'Assets\Resources\MechanicalCatalog\BicycleInteractionCatalog.json'
$bom = Get-Content -Raw -LiteralPath $bomPath | ConvertFrom-Json
$model = Get-Content -Raw -LiteralPath $modelPath | ConvertFrom-Json

$assemblyOrder = @(
    'controls_cables', 'saddle_seatpost', 'pedals', 'brake_front', 'brake_rear',
    'chain', 'front_derailleur', 'rear_derailleur', 'wheel_front', 'wheel_rear',
    'crank_bottom_bracket', 'cockpit_headset', 'fork', 'frame'
)

$assemblyById = @{}
$componentsByAssembly = @{}
foreach ($assembly in $bom.assemblies) {
    $assemblyById[$assembly.id] = $assembly
    if ($assembly.components) {
        $componentsByAssembly[$assembly.id] = @($assembly.components)
    }
}

$objectsByAssembly = @{}
$objectsByComponent = @{}
$objectByPartId = @{}
foreach ($part in $model.parts) {
    $objectByPartId[$part.partId] = $part.object
    if (-not $objectsByAssembly.ContainsKey($part.assemblyId)) {
        $objectsByAssembly[$part.assemblyId] = [System.Collections.Generic.List[string]]::new()
    }
    $objectsByAssembly[$part.assemblyId].Add($part.object)
    $segments = $part.partId.Split('.')
    $componentKey = ($segments[0..2] -join '.')
    if (-not $objectsByComponent.ContainsKey($componentKey)) {
        $objectsByComponent[$componentKey] = [System.Collections.Generic.List[string]]::new()
    }
    $objectsByComponent[$componentKey].Add($part.object)
}

function Get-ToolKind([string]$tool) {
    if ($tool -eq 'hex_key') { return 'HexKey' }
    if ($tool -eq 'torx_key') { return 'TorxKey' }
    return 'Hand'
}

function Get-FunctionText([string]$id, [string]$name, [string]$assemblyName) {
    switch -Regex ($id) {
        'tire' { return '轮胎与地面接触，提供抓地力并吸收细小震动。' }
        'inner_tube|valve' { return '它负责保存和调节轮胎内的空气压力。' }
        '^rim$|rim_tape' { return '轮圈支撑轮胎，并把路面载荷传给辐条系统。' }
        'spoke|nipple' { return '它通过张力连接轮圈与花鼓，帮助轮组保持圆度和侧向稳定。' }
        'hub|freehub|pawl' { return '它位于车轮中心，支撑旋转并把驱动力或制动力传给轮组。' }
        'rotor' { return '碟片随车轮旋转，来令片夹紧它时把运动能量转化为热。' }
        'brake_pad|pad_' { return '它直接接触碟片产生摩擦，是会逐渐磨损的制动零件。' }
        'piston' { return '活塞把液压或气压转换为直线推力。' }
        'hose|olive|connector|compression_nut' { return '它连接液压系统并保持油路密封，使压力可以可靠传递。' }
        'lever|reservoir|diaphragm' { return '它把手指动作转换为液压，并为刹车油留出补偿空间。' }
        'caliper' { return '卡钳容纳活塞和来令片，并从两侧夹紧碟片。' }
        'chainring|cassette_sprocket' { return '不同齿数组合会改变踩踏力量与车轮转速之间的比例。' }
        'chain' { return '链条把牙盘的转动传递到后轮飞轮。' }
        'derailleur|cage|jockey|limit_screw|b_tension' { return '它引导链条移动或保持张力，让链条准确落在目标齿片上。' }
        'crank|pedal' { return '它承接双脚的踩踏力量，并把力量传给牙盘。' }
        'bottom_bracket' { return '中轴组件支撑曲柄旋转，同时把踩踏载荷传给车架。' }
        'bearing|bushing' { return '它支撑运动零件并减小摩擦，同时帮助零件保持正确位置。' }
        'seal|wiper|foam' { return '它阻挡灰尘和水分，并帮助润滑介质留在机构内部。' }
        'fork|stanchion|damper|air_|rebound|compression' { return '它属于避震前叉系统，用弹性元件吸收冲击、用阻尼控制运动速度。' }
        'handlebar|stem|headset|crown_race|top_cap|spacer' { return '它属于转向系统，把骑手的转向动作传给前叉和前轮。' }
        'saddle|seatpost' { return '它支撑骑手，并通过座管调节乘坐高度和位置。' }
        'shifter|shift_|cable|housing|ferrule' { return '它把手指的变速指令传给拨链器。' }
        'bolt|screw|nut|clip|pin|washer|cap|clamp|collar' { return '它负责定位、连接或防松，使相邻零件保持正确装配关系。' }
        'frame|hanger|protector|guide' { return '它属于车架结构或车架小件，负责承载、定位或保护其他系统。' }
        default { return "$name 是$assemblyName 中的组成零件，负责连接、支撑、传力或保护。" }
    }
}

function New-Step(
    [string]$id,
    [string]$displayName,
    [string]$tool,
    [string]$summary,
    [string]$mechanism,
    [string]$advanced,
    [string]$assemblyId,
    [string]$componentId,
    [string[]]$objectNames
) {
    return [ordered]@{
        id = $id
        displayName = $displayName
        tool = $tool
        simpleSummary = $summary
        mechanism = $mechanism
        advancedNote = $advanced
        assemblyId = $assemblyId
        componentId = $componentId
        objectNames = @($objectNames)
    }
}

$simple = [System.Collections.Generic.List[object]]::new()
$standard = [System.Collections.Generic.List[object]]::new()
$advanced = [System.Collections.Generic.List[object]]::new()
$explorationRepeatThreshold = 10

foreach ($assemblyId in $assemblyOrder) {
    $assembly = $assemblyById[$assemblyId]
    $sourceId = if ($assembly.inheritComponentsFrom) { $assembly.inheritComponentsFrom } else { $assemblyId }
    $components = $componentsByAssembly[$sourceId]
    $assemblyObjects = @($objectsByAssembly[$assemblyId] | Sort-Object)
    $simple.Add((New-Step "plan.simple.$assemblyId" $assembly.name 'Hand' `
        "$($assembly.name)是整车的一个主要机械模块。" `
        '先观察它与车架及相邻模块的连接，再将整个模块作为一个单元拆下。' `
        "该模块在进阶和探索等级中会继续展开为 $($components.Count) 类零件。" `
        $assemblyId '' $assemblyObjects))

    foreach ($component in $components) {
        $componentKey = "bike.$assemblyId.$($component.id)"
        $componentObjects = @($objectsByComponent[$componentKey] | Sort-Object)
        $functionText = Get-FunctionText $component.id $component.name $assembly.name
        $displayName = if ($component.quantity -gt 1) { "$($component.name)（$($component.quantity)件）" } else { $component.name }
        $standard.Add((New-Step "plan.standard.$assemblyId.$($component.id)" $displayName (Get-ToolKind $component.tool) `
            $functionText `
            "这一组共有 $($component.quantity) 件；进阶等级按同类零件成组操作。" `
            "维修边界：$($component.serviceBoundary)。探索等级会按模型中的实体逐件操作。" `
            $assemblyId $component.id $componentObjects))

        if ([int]$component.quantity -ge $explorationRepeatThreshold) {
            $advanced.Add((New-Step "plan.advanced.$assemblyId.$($component.id)" $displayName (Get-ToolKind $component.tool) `
                $functionText `
                "这一组共有 $($component.quantity) 件；探索等级将高重复零件作为一个整体操作。" `
                "该组绑定全部 $($component.quantity) 个模型实体，避免重复零件逐件操作。" `
                $assemblyId $component.id $componentObjects))
            continue
        }

        for ($index = 1; $index -le $component.quantity; $index++) {
            $width = if ($component.quantity -ge 100) { 3 } else { 2 }
            $suffix = $index.ToString(('0' * $width))
            $partId = "$componentKey.$suffix"
            if (-not $objectByPartId.ContainsKey($partId)) {
                throw "模型清单缺少逻辑零件 $partId"
            }
            $individualName = if ($component.quantity -gt 1) { "$($component.name) $index" } else { $component.name }
            $advanced.Add((New-Step $partId $individualName (Get-ToolKind $component.tool) `
                $functionText `
                "这是 $($assembly.name) 中第 $index/$($component.quantity) 个同类实体。" `
                "稳定零件 ID：$partId；维修边界：$($component.serviceBoundary)。" `
                $assemblyId $component.id @($objectByPartId[$partId])))
        }
    }
}

$payload = [ordered]@{
    schemaVersion = 1
    moduleId = $bom.moduleId
    plans = @(
        [ordered]@{ difficulty = 'Simple'; steps = @($simple) },
        [ordered]@{ difficulty = 'Standard'; steps = @($standard) },
        [ordered]@{ difficulty = 'Advanced'; steps = @($advanced) }
    )
}

$outputDirectory = Split-Path -Parent $outputPath
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
[System.IO.File]::WriteAllText($outputPath, ($payload | ConvertTo-Json -Depth 12), [System.Text.UTF8Encoding]::new($false))
Write-Output "Generated $outputPath"
Write-Output "Steps: Simple=$($simple.Count), Standard=$($standard.Count), Advanced=$($advanced.Count)"

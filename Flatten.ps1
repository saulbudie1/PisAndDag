param(
    # Also delete the dead files listed in $dead below, and un-suffix
    # GreatbowOfAPilgrimHoldout-1.cs. Off by default; git rm, so it is undoable.
    [switch]$Prune
)

# Flatten.ps1 — lift everything out of Content\ and nest the weapons.
#
# Run from the mod root:
#     cd "C:\Users\Admin\Documents\My Games\Terraria\tModLoader\ModSources\PisAndDag"
#     powershell -ExecutionPolicy Bypass -File .\Flatten.ps1
#
# It does two jobs and nothing else:
#   1. git mv every folder in Content\ up one level, then Items\Ranged and
#      Items\Melee down into Items\Weapons\.
#   2. Rewrite namespaces, usings and hard-coded asset paths in every .cs so the
#      code matches where the files now are.
#
# Why git mv rather than move: it stages the rename, keeps the history, and
# `git checkout -- .` puts the whole thing back if you don't like the result.
#
# It refuses to run on a dirty tree, and it prints what it could not fix.

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------- safety ----

if (-not (Test-Path .git)) {
    Write-Error "No .git here. Run this from the mod root."
}

if ((git status --porcelain).Length -gt 0) {
    Write-Error "Working tree is dirty. Commit or stash first — this rewrites ~100 files and you want a clean undo."
}

if (-not (Test-Path Content)) {
    Write-Error "No Content\ folder. Already flattened?"
}

# ------------------------------------------------------------------ prune ---
#
# Files that are already dead: emptied stubs, a class that lost its last caller,
# textures whose class was renamed, and one .txt I left behind by mistake. They
# all still compile into the mod today.

$dead = @(
    'Content\Particles\MaelstromShard.cs',
    'Content\Particles\MaelstromShard.png',
    'Content\Particles\MaelstromBloom.cs',
    'Content\Particles\MaelstromBloom.png',
    'Content\Particles\_rename_probe.txt',
    'Content\Projectiles\PADIdleHoldoutProjectile.cs',
    'Content\Projectiles\Ranged\GreatbowOfAPilgrimHoldout.cs',
    'Content\NPCs\PADStaggerNPC.cs',
    'Content\Projectiles\Ranged\PilgrimArrow.cs'
)

if ($Prune) {
    Write-Host "== pruning ==" -ForegroundColor Cyan
    foreach ($f in $dead) {
        if (Test-Path $f) { git rm -q -- $f; Write-Host "  removed $f" }
        else { Write-Host "  skip $f (not there)" }
    }
    # The live holdout is the -1 file only because the old one was still sitting
    # on the name. With that gone it can have its name back.
    $one = 'Content\Projectiles\Ranged\GreatbowOfAPilgrimHoldout-1.cs'
    if ((Test-Path $one) -and -not (Test-Path 'Content\Projectiles\Ranged\GreatbowOfAPilgrimHoldout.cs')) {
        git mv $one 'Content\Projectiles\Ranged\GreatbowOfAPilgrimHoldout.cs'
        Write-Host "  GreatbowOfAPilgrimHoldout-1.cs -> GreatbowOfAPilgrimHoldout.cs"
    }
} else {
    Write-Host "== dead files (run with -Prune to remove) ==" -ForegroundColor DarkGray
    foreach ($f in $dead) { if (Test-Path $f) { Write-Host "  $f" -ForegroundColor DarkGray } }
}

Write-Host "== moving folders ==" -ForegroundColor Cyan

# ----------------------------------------------------------------- moves ----
#
# Whole folders, not individual files: a .png has to stay beside its .cs,
# because tModLoader builds the texture path out of the class's NAMESPACE
# (ModItem.Texture is literally Namespace + "." + Name with dots swapped for
# slashes). Move one without the other and the mod fails to load.

$top = @('Buffs','ExtraTextures','Items','NPCs','PADplayer','Particles',
         'Projectiles','Sounds','Structures','Tiles','UI')

foreach ($f in $top) {
    $src = "Content\$f"
    if (-not (Test-Path $src)) { Write-Host "  skip $src (not there)"; continue }
    if (Test-Path $f) { Write-Error "  $f already exists at the root. Stopping rather than merging blind." }
    git mv $src $f
    Write-Host "  $src -> $f"
}

# Anything left in Content\ was not in the list above — stray files go too.
if (Test-Path Content) {
    $leftovers = Get-ChildItem Content -Recurse -File
    if ($leftovers.Count -gt 0) {
        Write-Host "  Content\ still holds:" -ForegroundColor Yellow
        $leftovers | ForEach-Object { Write-Host "    $($_.FullName.Substring($PWD.Path.Length+1))" }
        Write-Host "  Move those by hand, then delete Content\." -ForegroundColor Yellow
    } else {
        Remove-Item Content -Recurse -Force
        Write-Host "  removed empty Content\"
    }
}

Write-Host "== nesting the weapons ==" -ForegroundColor Cyan

if (-not (Test-Path 'Items\Weapons')) { New-Item -ItemType Directory 'Items\Weapons' | Out-Null }

foreach ($w in @('Ranged','Melee')) {
    $src = "Items\$w"
    if (-not (Test-Path $src)) { Write-Host "  skip $src (not there)"; continue }
    git mv $src "Items\Weapons\$w"
    Write-Host "  $src -> Items\Weapons\$w"
}

# ---------------------------------------------------------------- rewrite ----
#
# Order matters. Strip Content first so everything is on one shape, THEN insert
# Weapons — and only into Items, never into Projectiles, which has its own
# Ranged and Melee folders that are NOT moving.

Write-Host "== rewriting sources ==" -ForegroundColor Cyan

# Our own folder names. Used to catch bare `Content.Items.X` references without
# touching Terraria.GameContent.* — there is no word boundary inside
# "GameContent", so \bContent\. cannot match it.
$ours = 'Items|NPCs|Projectiles|Particles|Structures|Tiles|UI|Buffs|PADplayer|ExtraTextures|Sounds'

$rules = @(
    # --- drop the Content level -------------------------------------------
    @{ From = 'PisAndDag\.Content\.';            To = 'PisAndDag.' },
    @{ From = 'namespace\s+PisAndDag\.Content\b'; To = 'namespace PisAndDag' },
    @{ From = "\bContent\.($ours)\b";            To = '$1' },
    @{ From = 'PisAndDag/Content/';              To = 'PisAndDag/' },
    # Mod.GetFileBytes and friends take a path relative to the mod root, with no
    # "PisAndDag/" on the front. Those literals need the same haircut.
    @{ From = '"Content/';                       To = '"' },

    # --- and nest the weapons ----------------------------------------------
    @{ From = 'PisAndDag\.Items\.(Ranged|Melee)\b'; To = 'PisAndDag.Items.Weapons.$1' },
    @{ From = '\bItems\.(Ranged|Melee)\b';          To = 'Items.Weapons.$1' },
    @{ From = 'PisAndDag/Items/(Ranged|Melee)/';    To = 'PisAndDag/Items/Weapons/$1/' },
    @{ From = '"Items/(Ranged|Melee)/';             To = '"Items/Weapons/$1/' },

    # --- and one typo that predates all of this ----------------------------
    # ExtraLargeWorld points at "ExtraTexture/GrandWorldSize" but the folder is
    # ExtraTextures, plural. That is the warning in client.log about the Grand
    # button wearing vanilla's Large icon.
    @{ From = 'ExtraTexture/';                      To = 'ExtraTextures/' }
)

$touched = 0

Get-ChildItem -Recurse -Include *.cs |
    Where-Object { $_.FullName -notmatch '\\(obj|bin|\.vs|Tools)\\' } |
    ForEach-Object {
        $path = $_.FullName
        $text = Get-Content -Raw -Encoding UTF8 $path
        $before = $text

        foreach ($r in $rules) { $text = $text -replace $r.From, $r.To }

        if ($text -ne $before) {
            # No BOM: tModLoader's compiler is fine either way, but a BOM appearing
            # on a hundred files makes the diff unreadable.
            [System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($false)))
            $touched++
            Write-Host "  $($path.Substring($PWD.Path.Length+1))"
        }
    }

Write-Host "  $touched files rewritten"

# ---------------------------------------------------------------- leftovers ---
#
# Anything still naming Content is something the rules did not have a shape for.
# Better to be told than to find it at load time.

Write-Host "== leftovers ==" -ForegroundColor Cyan

$stragglers = Get-ChildItem -Recurse -Include *.cs,*.hjson,*.csproj |
    Where-Object { $_.FullName -notmatch '\\(obj|bin|\.vs|Tools)\\' } |
    Select-String -Pattern 'PisAndDag[./]Content', '"Content/', '\bContent\.(Items|NPCs|Projectiles|Particles|Structures|Tiles|UI|Buffs|PADplayer)\b'

if ($stragglers) {
    Write-Host "  still referring to Content — fix these by hand:" -ForegroundColor Yellow
    $stragglers | ForEach-Object { Write-Host "    $($_.Path.Substring($PWD.Path.Length+1)):$($_.LineNumber)  $($_.Line.Trim())" }
} else {
    Write-Host "  none" -ForegroundColor Green
}

Write-Host ""
Write-Host "Done. Build the mod before you commit." -ForegroundColor Cyan
Write-Host "To undo everything:  git checkout -- . ; git reset" -ForegroundColor DarkGray

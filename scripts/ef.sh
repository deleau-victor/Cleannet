#!/usr/bin/env bash
#
# ef.sh — Wrapper interactif pour `dotnet ef`
# Utilise un IDesignTimeDbContextFactory (pas besoin de startup project)
# Dépendances : dotnet-ef, fzf
#
# Usage : ./ef.sh [--dry-run]
#

set -euo pipefail

DRY_RUN=false
[[ "${1:-}" == "--dry-run" ]] && DRY_RUN=true

# ─── Couleurs ────────────────────────────────────────────────────────────────
readonly C_RESET='\033[0m'
readonly C_BOLD='\033[1m'
readonly C_BLUE='\033[34m'
readonly C_GREEN='\033[32m'
readonly C_YELLOW='\033[33m'
readonly C_RED='\033[31m'

info()  { printf "${C_BLUE}▸${C_RESET} %s\n" "$*"; }
ok()    { printf "${C_GREEN}✓${C_RESET} %s\n" "$*"; }
warn()  { printf "${C_YELLOW}⚠${C_RESET} %s\n" "$*"; }
err()   { printf "${C_RED}✗${C_RESET} %s\n" "$*" >&2; }

# ─── Checks préalables ───────────────────────────────────────────────────────
command -v fzf >/dev/null 2>&1 || { err "fzf n'est pas installé (sudo dnf install fzf)"; exit 1; }
command -v dotnet >/dev/null 2>&1 || { err "dotnet n'est pas installé"; exit 1; }
dotnet ef --version >/dev/null 2>&1 || { err "dotnet-ef n'est pas installé (dotnet tool install --global dotnet-ef)"; exit 1; }

# ─── Helpers ─────────────────────────────────────────────────────────────────

# Trouve tous les .csproj du repo
find_projects() {
    find . -type f -name "*.csproj" \
        -not -path "*/bin/*" \
        -not -path "*/obj/*" \
        -not -path "*/node_modules/*" 2>/dev/null | sed 's|^\./||'
}

# Sélection du projet d'infrastructure (contient le DbContext + la factory)
pick_infra_project() {
    local projects
    projects=$(find_projects)
    [[ -z "$projects" ]] && { err "Aucun .csproj trouvé"; exit 1; }

    # Priorise les projets qui ressemblent à de l'infra
    local sorted
    sorted=$(echo "$projects" | awk '
        /[Ii]nfrastructure|[Pp]ersistence|[Dd]ata|[Dd]atabase/ { print "1\t" $0; next }
        { print "2\t" $0 }
    ' | sort -k1,1 | cut -f2-)

    echo "$sorted" | fzf \
        --height=40% \
        --border=rounded \
        --prompt="Projet infrastructure ❯ " \
        --header="Sélectionne le projet contenant le DbContext + DesignTimeDbContextFactory" \
        --preview='head -50 {}' \
        --preview-window=right:50%:wrap
}

# Liste les migrations existantes
list_migrations() {
    local project="$1"
    dotnet ef migrations list \
        --project "$project" \
        --no-build 2>/dev/null \
        | grep -vE "^(Build started|Build succeeded|info:|warn:)" \
        | grep -E "^[0-9]{14}_" || true
}

# Sélection d'une migration
pick_migration() {
    local project="$1" prompt="$2"
    local migrations
    migrations=$(list_migrations "$project")

    if [[ -z "$migrations" ]]; then
        err "Aucune migration trouvée dans $project"
        return 1
    fi

    # Option "0" pour revert tout (utile pour Update-Database 0)
    { echo "0  (revert toutes les migrations)"; echo "$migrations"; } | \
        fzf --height=50% \
            --border=rounded \
            --prompt="$prompt ❯ " \
            --header="Tri chronologique — la plus récente en bas" \
            --tac
}

# Exécute ou affiche la commande selon --dry-run
run_cmd() {
    printf "\n${C_BOLD}${C_BLUE}$ %s${C_RESET}\n\n" "$*"
    if $DRY_RUN; then
        warn "dry-run : commande non exécutée"
        return 0
    fi
    "$@"
}

# ─── Actions ─────────────────────────────────────────────────────────────────

action_add() {
    local project name
    project=$(pick_infra_project) || return 1

    read -rp "$(printf "${C_BOLD}Nom de la migration${C_RESET} (PascalCase, ex: AddUserTable) : ")" name
    [[ -z "$name" ]] && { err "Nom requis"; return 1; }

    if [[ "$name" =~ [[:space:]] ]]; then
        err "Le nom ne doit pas contenir d'espaces"
        return 1
    fi

    run_cmd dotnet ef migrations add "$name" --project "$project"
}

action_remove() {
    local project
    project=$(pick_infra_project) || return 1

    warn "Supprime la DERNIÈRE migration (si non appliquée en base)"
    read -rp "Continuer ? [y/N] " confirm
    [[ "$confirm" =~ ^[yY]$ ]] || { info "Annulé"; return 0; }

    run_cmd dotnet ef migrations remove --project "$project"
}

action_update() {
    local project choice target
    project=$(pick_infra_project) || return 1

    choice=$(printf "latest  (dernière migration)\nspecific  (migration précise)" | \
        fzf --height=20% --border=rounded --prompt="Cible ❯ ") || return 1

    if [[ "$choice" == latest* ]]; then
        run_cmd dotnet ef database update --project "$project"
    else
        target=$(pick_migration "$project" "Migration cible") || return 1
        target=$(echo "$target" | awk '{print $1}')
        run_cmd dotnet ef database update "$target" --project "$project"
    fi
}

action_revert() {
    local project target
    project=$(pick_infra_project) || return 1

    info "Sélectionne la migration jusqu'à laquelle revenir (0 = tout annuler)"
    target=$(pick_migration "$project" "Revert jusqu'à") || return 1
    target=$(echo "$target" | awk '{print $1}')

    warn "Tu vas revert la base jusqu'à : $target"
    read -rp "Continuer ? [y/N] " confirm
    [[ "$confirm" =~ ^[yY]$ ]] || { info "Annulé"; return 0; }

    run_cmd dotnet ef database update "$target" --project "$project"
}

action_list() {
    local project
    project=$(pick_infra_project) || return 1

    run_cmd dotnet ef migrations list --project "$project"
}

action_script() {
    local project from to output
    project=$(pick_infra_project) || return 1

    info "Génère un script SQL idempotent entre deux migrations"

    from=$(pick_migration "$project" "Depuis (laisse vide = début)") || return 1
    from=$(echo "$from" | awk '{print $1}')

    to=$(pick_migration "$project" "Jusqu'à (laisse vide = dernière)") || return 1
    to=$(echo "$to" | awk '{print $1}')

    output="./migration-${from}-to-${to}.sql"

    run_cmd dotnet ef migrations script "$from" "$to" \
        --project "$project" \
        --idempotent \
        --output "$output"

    $DRY_RUN || ok "Script généré : $output"
}

action_drop() {
    local project
    project=$(pick_infra_project) || return 1

    err "⚠️  DROP DATABASE — toutes les données seront perdues"
    read -rp "Tape 'DROP' pour confirmer : " confirm
    [[ "$confirm" == "DROP" ]] || { info "Annulé"; return 0; }

    run_cmd dotnet ef database drop --project "$project" --force
}

# ─── Menu principal ──────────────────────────────────────────────────────────

main() {
    printf "${C_BOLD}${C_BLUE}╭─ dotnet ef migrate ─╮${C_RESET}\n"
    $DRY_RUN && warn "Mode dry-run activé"
    echo

    local action
    action=$(cat <<EOF | fzf --height=50% --border=rounded --prompt="Action ❯ " --header="Que veux-tu faire ?"
add       Créer une nouvelle migration
update    Mettre à jour la base (latest ou migration précise)
revert    Revert vers une migration antérieure
remove    Supprimer la dernière migration (non appliquée)
list      Lister toutes les migrations
script    Générer un script SQL idempotent
drop      Supprimer la base de données
EOF
) || { info "Annulé"; exit 0; }

    local cmd="${action%% *}"
    case "$cmd" in
        add)    action_add ;;
        update) action_update ;;
        revert) action_revert ;;
        remove) action_remove ;;
        list)   action_list ;;
        script) action_script ;;
        drop)   action_drop ;;
        *)      err "Action inconnue : $cmd"; exit 1 ;;
    esac
}

main "$@"

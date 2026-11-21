using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AgVerse
{
    public static class Localization
    {
        public static string CurrentLanguage { get; private set; } = "en";

        public static readonly Dictionary<string, string> LanguageOptions = new()
        {
            { "English", "en" },
            { "Português", "pt" },
            { "Español", "es" },
            { "Русский", "ru" }
        };

        private static readonly Dictionary<string, Dictionary<string, string>> Strings = new()
        {
            ["en"] = new()
            {
                ["title"] = "AgVerse - Multi-Profile Manager for Antigravity",
                ["frame_create"] = "Create or Rename Profile",
                ["lbl_nome"] = "Profile Name:",
                ["btn_save"] = "Save",
                ["btn_add"] = "Add",
                ["info_default_shared"] = "ℹ All projects are shared by default. Exclusions can be configured per profile.",
                ["frame_perfis"] = "Profiles",
                ["btn_abrir"] = "Launch Selected Profile",
                ["btn_proj"] = "Manage Projects",
                ["btn_atalho"] = "Create Desktop Shortcut",
                ["btn_del"] = "Delete Profile",
                ["btn_refresh"] = "Refresh",
                ["btn_refresh_token"] = "Refresh Token",
                ["btn_set_default"] = "⭐ Set as Default (IDE Active)",
                ["badge_default"] = "⭐ [Default]",
                ["msg_default_set"] = "Profile '{0}' is now set as the active default IDE profile!",
                ["chk_verbose"] = "Verbose Mode (Conversation Steps: Verbose)",
                ["lbl_idioma"] = "Language:",
                ["projetos_sufixo"] = "projects",
                ["aviso_selecao"] = "Please select a profile from the list!",
                ["aviso_nome"] = "Please enter a profile name!",
                ["erro_existe"] = "A profile with this name already exists!",
                ["sucesso_criado"] = "Profile '{0}' created successfully!",
                ["sucesso_renomeado"] = "Profile '{0}' renamed to '{1}' successfully!",
                ["sucesso_atalho"] = "Shortcut created on Desktop:\nAntigravity - {0}.lnk",
                ["confirma_del"] = "Are you sure you want to delete profile '{0}' and all its isolated data?",
                ["top_titulo"] = "Project Exclusions - {0}",
                ["top_aviso"] = "Check projects to EXCLUDE (hide) from profile: {0}",
                ["top_aviso_sub"] = "(All other projects are shared across profiles automatically)",
                ["filtrar"] = "Filter:",
                ["marcar_todos"] = "Exclude All",
                ["desmarcar_todos"] = "Share All (Clear)",
                ["btn_salvar"] = "Save Changes",
                ["btn_cancelar"] = "Cancel",
                ["sucesso_proj"] = "Project exclusions for profile '{0}' updated successfully!",
                ["btn_archive"] = "Archive Globally",
                ["btn_unarchive"] = "Restore Project",
                ["btn_global_archive"] = "Project Archive (Global)",
                ["tab_exclusions"] = "Profile Exclusions",
                ["tab_archived"] = "Archived Projects ({0})",
                ["archived_sub"] = "Archived projects are hidden from all profiles. Restore to share again.",
                ["confirma_archive"] = "Are you sure you want to archive '{0}' globally?\nIt will be removed from Antigravity across ALL profiles.",
                ["confirma_unarchive"] = "Restore '{0}' to active status?\nIt will be available across all profiles again.",
                ["sucesso_archived"] = "Project '{0}' has been archived from all profiles.",
                ["sucesso_unarchived"] = "Project '{0}' has been restored to all profiles.",
                ["info_sem_archived"] = "No archived projects found.",
                ["info_sem_proj"] = "No projects found on the system!",
                ["t_aviso"] = "Warning",
                ["t_erro"] = "Error",
                ["t_sucesso"] = "Success",
                ["t_confirma"] = "Confirm",
                ["t_info"] = "Information"
            },
            ["pt"] = new()
            {
                ["title"] = "AgVerse - Gerenciador de Perfis Antigravity",
                ["frame_create"] = "Criar ou Renomear Perfil",
                ["lbl_nome"] = "Nome do Perfil:",
                ["btn_save"] = "Salvar",
                ["btn_add"] = "Adicionar",
                ["info_default_shared"] = "ℹ Todos os projetos são compartilhados por padrão. Exclusões são gerenciadas por perfil.",
                ["frame_perfis"] = "Perfis",
                ["btn_abrir"] = "Abrir Perfil Selecionado",
                ["btn_proj"] = "Gerenciar Projetos",
                ["btn_atalho"] = "Criar Atalho na Area de Trabalho",
                ["btn_del"] = "Excluir Perfil",
                ["btn_refresh"] = "Atualizar",
                ["btn_refresh_token"] = "Atualizar Token",
                ["btn_set_default"] = "⭐ Definir como Padrão (IDE Ativo)",
                ["badge_default"] = "⭐ [Padrão]",
                ["msg_default_set"] = "O perfil '{0}' agora está definido como o perfil padrão ativo da IDE!",
                ["chk_verbose"] = "Modo Detalhado (Passos da Conversa: Verbose)",
                ["lbl_idioma"] = "Idioma:",
                ["projetos_sufixo"] = "projetos",
                ["aviso_selecao"] = "Selecione um perfil na lista!",
                ["aviso_nome"] = "Digite um nome para o perfil!",
                ["erro_existe"] = "Já existe um perfil com esse nome!",
                ["sucesso_criado"] = "Perfil '{0}' criado com sucesso!",
                ["sucesso_renomeado"] = "Perfil '{0}' renomeado para '{1}' com sucesso!",
                ["sucesso_atalho"] = "Atalho criado na Área de Trabalho:\nAntigravity - {0}.lnk",
                ["confirma_del"] = "Tem certeza que deseja apagar o perfil '{0}' e todos os dados dele?",
                ["top_titulo"] = "Exclusões de Projetos - {0}",
                ["top_aviso"] = "Marque os projetos para EXCLUIR (ocultar) de: {0}",
                ["top_aviso_sub"] = "(Todos os outros projetos são compartilhados automaticamente)",
                ["filtrar"] = "Filtrar:",
                ["marcar_todos"] = "Excluir Todos",
                ["desmarcar_todos"] = "Compartilhar Todos",
                ["btn_salvar"] = "Salvar Alterações",
                ["btn_cancelar"] = "Cancelar",
                ["sucesso_proj"] = "Exclusões do perfil '{0}' atualizadas com sucesso!",
                ["btn_archive"] = "Arquivar Globalmente",
                ["btn_unarchive"] = "Restaurar Projeto",
                ["btn_global_archive"] = "Arquivo de Projetos (Global)",
                ["tab_exclusions"] = "Exclusões do Perfil",
                ["tab_archived"] = "Projetos Arquivados ({0})",
                ["archived_sub"] = "Projetos arquivados estão ocultos de todos os perfis. Restaure para compartilhar novamente.",
                ["confirma_archive"] = "Tem certeza que deseja arquivar '{0}' globalmente?\nEle será removido do Antigravity em TODOS os perfis.",
                ["confirma_unarchive"] = "Restaurar '{0}' para o status ativo?\nEle voltará a ser compartilhado em todos os perfis.",
                ["sucesso_archived"] = "Projeto '{0}' arquivado com sucesso de todos os perfis.",
                ["sucesso_unarchived"] = "Projeto '{0}' restaurado para todos os perfis.",
                ["info_sem_archived"] = "Nenhum projeto arquivado encontrado.",
                ["info_sem_proj"] = "Nenhum projeto encontrado no sistema!",
                ["t_aviso"] = "Aviso",
                ["t_erro"] = "Erro",
                ["t_sucesso"] = "Sucesso",
                ["t_confirma"] = "Confirmar",
                ["t_info"] = "Info"
            },
            ["es"] = new()
            {
                ["title"] = "AgVerse - Administrador de Perfiles Antigravity",
                ["frame_create"] = "Crear o Renombrar Perfil",
                ["lbl_nome"] = "Nombre del Perfil:",
                ["btn_save"] = "Guardar",
                ["btn_add"] = "Agregar",
                ["info_default_shared"] = "ℹ Todos los proyectos se comparten por defecto. Exclusiones configurables por perfil.",
                ["frame_perfis"] = "Perfiles",
                ["btn_abrir"] = "Abrir Perfil Seleccionado",
                ["btn_proj"] = "Administrar Proyectos",
                ["btn_atalho"] = "Crear Acceso Directo en el Escritorio",
                ["btn_del"] = "Eliminar Perfil",
                ["btn_refresh"] = "Actualizar",
                ["btn_refresh_token"] = "Actualizar Token",
                ["btn_set_default"] = "⭐ Establecer Predeterminado (IDE Activo)",
                ["badge_default"] = "⭐ [Predeterminado]",
                ["msg_default_set"] = "¡El perfil '{0}' ahora está establecido como el perfil predeterminado activo del IDE!",
                ["chk_verbose"] = "Modo Detallado (Pasos de Conversación: Verbose)",
                ["lbl_idioma"] = "Idioma:",
                ["projetos_sufixo"] = "proyectos",
                ["aviso_selecao"] = "¡Selecciona un perfil de la lista!",
                ["aviso_nome"] = "¡Introduce un nombre para el perfil!",
                ["erro_existe"] = "¡Ya existe un perfil con ese nombre!",
                ["sucesso_criado"] = "¡Perfil '{0}' creado con éxito!",
                ["sucesso_renomeado"] = "¡Perfil '{0}' renombrado a '{1}' con éxito!",
                ["sucesso_atalho"] = "Acceso directo creado en el Escritorio:\nAntigravity - {0}.lnk",
                ["confirma_del"] = "¿Estás seguro de que deseas eliminar el perfil '{0}' y todos sus datos?",
                ["top_titulo"] = "Exclusiones de Proyectos - {0}",
                ["top_aviso"] = "Marque los proyectos para EXCLUIR (ocultar) de: {0}",
                ["top_aviso_sub"] = "(Todos los demás proyectos se comparten automáticamente)",
                ["filtrar"] = "Filtrar:",
                ["marcar_todos"] = "Excluir Todos",
                ["desmarcar_todos"] = "Compartir Todos",
                ["btn_salvar"] = "Guardar Cambios",
                ["btn_cancelar"] = "Cancelar",
                ["sucesso_proj"] = "¡Exclusiones del perfil '{0}' actualizadas con éxito!",
                ["btn_archive"] = "Archivar Globalmente",
                ["btn_unarchive"] = "Restaurar Proyecto",
                ["btn_global_archive"] = "Archivo de Proyectos (Global)",
                ["tab_exclusions"] = "Exclusiones del Perfil",
                ["tab_archived"] = "Proyectos Archivados ({0})",
                ["archived_sub"] = "Los proyectos archivados están ocultos de todos los perfiles. Restáurelos para compartir de nuevo.",
                ["confirma_archive"] = "¿Está seguro de archivar '{0}' globalmente?\nSe eliminará de Antigravity en TODOS los perfiles.",
                ["confirma_unarchive"] = "¿Restaurar '{0}' al estado activo?\nEstará disponible de nuevo en todos los perfiles.",
                ["sucesso_archived"] = "Proyecto '{0}' archivado de todos los perfiles.",
                ["sucesso_unarchived"] = "Proyecto '{0}' restaurado a todos los perfiles.",
                ["info_sem_archived"] = "No se encontraron proyectos archivados.",
                ["info_sem_proj"] = "¡No se encontraron proyectos en el sistema!",
                ["t_aviso"] = "Aviso",
                ["t_erro"] = "Error",
                ["t_sucesso"] = "Éxito",
                ["t_confirma"] = "Confirmar",
                ["t_info"] = "Info"
            },
            ["ru"] = new()
            {
                ["title"] = "AgVerse - Менеджер профилей Antigravity",
                ["frame_create"] = "Создать или переименовать профиль",
                ["lbl_nome"] = "Имя профиля:",
                ["btn_save"] = "Сохранить",
                ["btn_add"] = "Добавить",
                ["info_default_shared"] = "ℹ Все проекты доступны по умолчанию. Исключения настраиваются для каждого профиля.",
                ["frame_perfis"] = "Профили",
                ["btn_abrir"] = "Открыть выбранный профиль",
                ["btn_proj"] = "Управление проектами",
                ["btn_atalho"] = "Создать ярлык на рабочем столе",
                ["btn_del"] = "Удалить профиль",
                ["btn_refresh"] = "Обновить",
                ["btn_refresh_token"] = "Обновить токен",
                ["btn_set_default"] = "⭐ Сделать по умолч. (IDE активен)",
                ["badge_default"] = "⭐ [По умолч.]",
                ["msg_default_set"] = "Профиль '{0}' установлен как активный профиль IDE по умолчанию!",
                ["chk_verbose"] = "Подробный режим (Шаги диалога: Verbose)",
                ["lbl_idioma"] = "Язык:",
                ["projetos_sufixo"] = "проектов",
                ["aviso_selecao"] = "Выберите профиль из списка!",
                ["aviso_nome"] = "Введите имя для профиля!",
                ["erro_existe"] = "Профиль с таким именем уже существует!",
                ["sucesso_criado"] = "Профиль '{0}' успешно создан!",
                ["sucesso_renomeado"] = "Профиль '{0}' успешно переименован в '{1}'!",
                ["sucesso_atalho"] = "Ярлык создан на рабочем столе:\nAntigravity - {0}.lnk",
                ["confirma_del"] = "Вы уверены, что хотите удалить профиль '{0}' и все его данные?",
                ["top_titulo"] = "Исключения проектов - {0}",
                ["top_aviso"] = "Отметьте проекты, которые нужно ИСКЛЮЧИТЬ (скрыть) для: {0}",
                ["top_aviso_sub"] = "(Все остальные проекты доступны автоматически)",
                ["filtrar"] = "Фильтр:",
                ["marcar_todos"] = "Исключить все",
                ["desmarcar_todos"] = "Сделать доступными все",
                ["btn_salvar"] = "Сохранить изменения",
                ["btn_cancelar"] = "Отмена",
                ["sucesso_proj"] = "Исключения проектов профиля '{0}' успешно обновлены!",
                ["btn_archive"] = "Архивировать глобально",
                ["btn_unarchive"] = "Восстановить проект",
                ["btn_global_archive"] = "Архив проектов (глобально)",
                ["tab_exclusions"] = "Исключения профиля",
                ["tab_archived"] = "Архив проектов ({0})",
                ["archived_sub"] = "Архивированные проекты скрыты во всех профилях. Восстановите для повторного открытия.",
                ["confirma_archive"] = "Вы уверены, что хотите архивировать '{0}' глобально?\nОн будет удален из Antigravity во ВСЕХ профилях.",
                ["confirma_unarchive"] = "Восстановить проект '{0}'?\nОн снова станет доступен во всех профилях.",
                ["sucesso_archived"] = "Проект '{0}' успешно архивирован.",
                ["sucesso_unarchived"] = "Проект '{0}' успешно восстановлен.",
                ["info_sem_archived"] = "Архивированные проекты отсутствуют.",
                ["info_sem_proj"] = "Проекты в системе не найдены!",
                ["t_aviso"] = "Предупреждение",
                ["t_erro"] = "Ошибка",
                ["t_sucesso"] = "Успех",
                ["t_confirma"] = "Подтверждение",
                ["t_info"] = "Информация"
            }
        };

        public static void Initialize(string configFile)
        {
            try
            {
                if (File.Exists(configFile))
                {
                    string json = File.ReadAllText(configFile);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("lang", out var langProp))
                    {
                        string? lang = langProp.GetString();
                        if (!string.IsNullOrEmpty(lang) && Strings.ContainsKey(lang))
                        {
                            CurrentLanguage = lang;
                            return;
                        }
                    }
                }
            }
            catch { }

            // Default fallback
            CurrentLanguage = "en";
        }

        public static void SetLanguage(string lang, string configFile)
        {
            if (Strings.ContainsKey(lang))
            {
                CurrentLanguage = lang;
                try
                {
                    AntigravityCore.SaveConfigProperty("lang", System.Text.Json.Nodes.JsonValue.Create(lang));
                }
                catch { }
            }
        }

        public static string T(string key, params object[] args)
        {
            var dict = Strings.GetValueOrDefault(CurrentLanguage, Strings["en"]);
            string val = dict.GetValueOrDefault(key, key);
            if (args != null && args.Length > 0)
            {
                return string.Format(val, args);
            }
            return val;
        }
    }
}

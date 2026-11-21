using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MultiGravity
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
                ["title"] = "MultiGravity - Multi-Profile Manager for Antigravity",
                ["frame_create"] = "Create New Profile",
                ["lbl_nome"] = "Profile Name:",
                ["btn_add"] = "Add",
                ["info_default_shared"] = "ℹ All projects are shared by default. Exclusions can be configured per profile.",
                ["frame_perfis"] = "Profiles",
                ["btn_abrir"] = "Launch Selected Profile",
                ["btn_proj"] = "Manage Project Exclusions",
                ["btn_atalho"] = "Create Desktop Shortcut",
                ["btn_del"] = "Delete Profile",
                ["lbl_idioma"] = "Language:",
                ["projetos_sufixo"] = "projects",
                ["aviso_selecao"] = "Please select a profile from the list!",
                ["aviso_nome"] = "Please enter a profile name!",
                ["erro_existe"] = "A profile with this name already exists!",
                ["sucesso_criado"] = "Profile '{0}' created successfully!",
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
                ["info_sem_proj"] = "No projects found on the system!",
                ["t_aviso"] = "Warning",
                ["t_erro"] = "Error",
                ["t_sucesso"] = "Success",
                ["t_confirma"] = "Confirm",
                ["t_info"] = "Information"
            },
            ["pt"] = new()
            {
                ["title"] = "MultiGravity - Gerenciador de Perfis Antigravity",
                ["frame_create"] = "Criar Novo Perfil",
                ["lbl_nome"] = "Nome do Perfil:",
                ["btn_add"] = "Adicionar",
                ["info_default_shared"] = "ℹ Todos os projetos são compartilhados por padrão. Exclusões são gerenciadas por perfil.",
                ["frame_perfis"] = "Perfis",
                ["btn_abrir"] = "Abrir Perfil Selecionado",
                ["btn_proj"] = "Gerenciar Exclusões de Projetos",
                ["btn_atalho"] = "Criar Atalho na Area de Trabalho",
                ["btn_del"] = "Excluir Perfil",
                ["lbl_idioma"] = "Idioma:",
                ["projetos_sufixo"] = "projetos",
                ["aviso_selecao"] = "Selecione um perfil na lista!",
                ["aviso_nome"] = "Digite um nome para o perfil!",
                ["erro_existe"] = "Já existe um perfil com esse nome!",
                ["sucesso_criado"] = "Perfil '{0}' criado com sucesso!",
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
                ["info_sem_proj"] = "Nenhum projeto encontrado no sistema!",
                ["t_aviso"] = "Aviso",
                ["t_erro"] = "Erro",
                ["t_sucesso"] = "Sucesso",
                ["t_confirma"] = "Confirmar",
                ["t_info"] = "Info"
            },
            ["es"] = new()
            {
                ["title"] = "MultiGravity - Administrador de Perfiles Antigravity",
                ["frame_create"] = "Crear Nuevo Perfil",
                ["lbl_nome"] = "Nombre del Perfil:",
                ["btn_add"] = "Agregar",
                ["info_default_shared"] = "ℹ Todos los proyectos se comparten por defecto. Exclusiones configurables por perfil.",
                ["frame_perfis"] = "Perfiles",
                ["btn_abrir"] = "Abrir Perfil Seleccionado",
                ["btn_proj"] = "Administrar Exclusiones de Proyectos",
                ["btn_atalho"] = "Crear Acceso Directo en el Escritorio",
                ["btn_del"] = "Eliminar Perfil",
                ["lbl_idioma"] = "Idioma:",
                ["projetos_sufixo"] = "proyectos",
                ["aviso_selecao"] = "¡Selecciona un perfil de la lista!",
                ["aviso_nome"] = "¡Introduce un nombre para el perfil!",
                ["erro_existe"] = "¡Ya existe un perfil con ese nombre!",
                ["sucesso_criado"] = "¡Perfil '{0}' creado con éxito!",
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
                ["info_sem_proj"] = "¡No se encontraron proyectos en el sistema!",
                ["t_aviso"] = "Aviso",
                ["t_erro"] = "Error",
                ["t_sucesso"] = "Éxito",
                ["t_confirma"] = "Confirmar",
                ["t_info"] = "Info"
            },
            ["ru"] = new()
            {
                ["title"] = "MultiGravity - Менеджер профилей Antigravity",
                ["frame_create"] = "Создать новый профиль",
                ["lbl_nome"] = "Имя профиля:",
                ["btn_add"] = "Добавить",
                ["info_default_shared"] = "ℹ Все проекты доступны по умолчанию. Исключения настраиваются для каждого профиля.",
                ["frame_perfis"] = "Профили",
                ["btn_abrir"] = "Открыть выбранный профиль",
                ["btn_proj"] = "Управление исключениями проектов",
                ["btn_atalho"] = "Создать ярлык на рабочем столе",
                ["btn_del"] = "Удалить профиль",
                ["lbl_idioma"] = "Язык:",
                ["projetos_sufixo"] = "проектов",
                ["aviso_selecao"] = "Выберите профиль из списка!",
                ["aviso_nome"] = "Введите имя для профиля!",
                ["erro_existe"] = "Профиль с таким именем уже существует!",
                ["sucesso_criado"] = "Профиль '{0}' успешно создан!",
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
                    string json = JsonSerializer.Serialize(new { lang });
                    File.WriteAllText(configFile, json);
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

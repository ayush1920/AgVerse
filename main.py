import os
import sys
import json
import shutil
import subprocess
import tkinter as tk
from tkinter import messagebox

if getattr(sys, "frozen", False):
    BASE_DIR = os.path.dirname(sys.executable)
else:
    BASE_DIR = os.path.dirname(os.path.abspath(__file__))

PROFILES_DIR = os.path.join(BASE_DIR, "profiles")
CONFIG_FILE = os.path.join(BASE_DIR, "config.json")
MASTER_DIR = os.path.join(os.path.expanduser("~"), ".gemini", "config", "projects")

def get_antigravity_exe():
    local = os.environ.get("LOCALAPPDATA", "")
    p1 = os.path.join(local, "Programs", "antigravity", "Antigravity.exe")
    if os.path.exists(p1):
        return p1
    prog = os.environ.get("PROGRAMFILES", "C:\\Program Files")
    p2 = os.path.join(prog, "Antigravity", "Antigravity.exe")
    if os.path.exists(p2):
        return p2
    return p1

EXE_PATH = get_antigravity_exe()

os.makedirs(PROFILES_DIR, exist_ok=True)

TEXTOS = {
    "pt": {
        "title": "Gerenciador de Perfis Antigravity",
        "frame_create": "Criar Novo Perfil",
        "lbl_nome": "Nome:",
        "btn_add": "Adicionar",
        "chk_copiar": "Todos os projetos compartilhados por padrão",
        "frame_perfis": "Perfis",
        "btn_abrir": "Abrir Perfil Selecionado",
        "btn_proj": "Gerenciar Exclusões de Projetos",
        "btn_atalho": "Criar Atalho na Area de Trabalho",
        "btn_del": "Excluir Perfil",
        "lbl_idioma": "Idioma:",
        "projetos_sufixo": "projetos",
        "aviso_selecao": "Selecione um perfil na lista!",
        "aviso_nome": "Digite um nome para o perfil!",
        "erro_existe": "Ja existe um perfil com esse nome!",
        "sucesso_criado": "Perfil '{nome}' criado com sucesso!",
        "sucesso_atalho": "Atalho criado na Area de Trabalho:\nAntigravity - {nome}.lnk",
        "confirma_del": "Tem certeza que deseja apagar o perfil '{nome}' e todos os dados dele?",
        "top_titulo": "Exclusões de Projetos - {nome}",
        "top_aviso": "Marque os projetos para EXCLUIR (ocultar) de: {nome}\n(Todos os outros projetos são compartilhados por padrão)",
        "filtrar": "Filtrar:",
        "marcar_todos": "Excluir Todos",
        "desmarcar_todos": "Compartilhar Todos",
        "btn_salvar": "Salvar Alteracoes",
        "btn_cancelar": "Cancelar",
        "sucesso_proj": "Exclusões do perfil '{nome}' atualizadas com sucesso!",
        "info_sem_proj": "Nenhum projeto encontrado no sistema!",
        "t_aviso": "Aviso",
        "t_erro": "Erro",
        "t_sucesso": "Sucesso",
        "t_confirma": "Confirmar",
        "t_info": "Info"
    },
    "en": {
        "title": "Antigravity Profile Manager",
        "frame_create": "Create New Profile",
        "lbl_nome": "Name:",
        "btn_add": "Add",
        "chk_copiar": "All projects shared by default",
        "frame_perfis": "Profiles",
        "btn_abrir": "Open Selected Profile",
        "btn_proj": "Manage Project Exclusions",
        "btn_atalho": "Create Desktop Shortcut",
        "btn_del": "Delete Profile",
        "lbl_idioma": "Language:",
        "projetos_sufixo": "projects",
        "aviso_selecao": "Select a profile from the list!",
        "aviso_nome": "Enter a name for the profile!",
        "erro_existe": "A profile with this name already exists!",
        "sucesso_criado": "Profile '{nome}' created successfully!",
        "sucesso_atalho": "Shortcut created on Desktop:\nAntigravity - {nome}.lnk",
        "confirma_del": "Are you sure you want to delete profile '{nome}' and all its data?",
        "top_titulo": "Project Exclusions - {nome}",
        "top_aviso": "Check projects to EXCLUDE (hide) from: {nome}\n(All other projects are shared by default)",
        "filtrar": "Filter:",
        "marcar_todos": "Exclude All",
        "desmarcar_todos": "Share All (Clear)",
        "btn_salvar": "Save Changes",
        "btn_cancelar": "Cancel",
        "sucesso_proj": "Project exclusions for '{nome}' updated successfully!",
        "info_sem_proj": "No projects found on system!",
        "t_aviso": "Warning",
        "t_erro": "Error",
        "t_sucesso": "Success",
        "t_confirma": "Confirm",
        "t_info": "Info"
    },
    "es": {
        "title": "Administrador de Perfiles Antigravity",
        "frame_create": "Crear Nuevo Perfil",
        "lbl_nome": "Nombre:",
        "btn_add": "Agregar",
        "chk_copiar": "Todos los proyectos compartidos por defecto",
        "frame_perfis": "Perfiles",
        "btn_abrir": "Abrir Perfil Seleccionado",
        "btn_proj": "Administrar Exclusiones de Proyectos",
        "btn_atalho": "Crear Acceso Directo en el Escritorio",
        "btn_del": "Eliminar Perfil",
        "lbl_idioma": "Idioma:",
        "projetos_sufixo": "proyectos",
        "aviso_selecao": "¡Selecciona un perfil de la lista!",
        "aviso_nome": "¡Introduce un nombre para el perfil!",
        "erro_existe": "¡Ya existe un perfil con ese nombre!",
        "sucesso_criado": "¡Perfil '{nome}' creado con exito!",
        "sucesso_atalho": "Acceso directo creado en el Escritorio:\nAntigravity - {nome}.lnk",
        "confirma_del": "¿Estas seguro de que deseas eliminar el perfil '{nome}' y todos sus datos?",
        "top_titulo": "Exclusiones de Proyectos - {nome}",
        "top_aviso": "Marque los proyectos para EXCLUIR (ocultar) de: {nome}\n(Todos los demás se comparten por defecto)",
        "filtrar": "Filtrar:",
        "marcar_todos": "Excluir Todos",
        "desmarcar_todos": "Compartir Todos",
        "btn_salvar": "Guardar Cambios",
        "btn_cancelar": "Cancelar",
        "sucesso_proj": "¡Exclusiones del perfil '{nome}' actualizadas con exito!",
        "info_sem_proj": "¡No se encontraron proyectos en el sistema!",
        "t_aviso": "Aviso",
        "t_erro": "Error",
        "t_sucesso": "Exito",
        "t_confirma": "Confirmar",
        "t_info": "Info"
    },
    "ru": {
        "title": "Менеджер профилей Antigravity",
        "frame_create": "Создать новый профиль",
        "lbl_nome": "Имя:",
        "btn_add": "Добавить",
        "chk_copiar": "Все проекты доступны по умолчанию",
        "frame_perfis": "Профили",
        "btn_abrir": "Открыть выбранный профиль",
        "btn_proj": "Управление исключениями проектов",
        "btn_atalho": "Создать ярлык на рабочем столе",
        "btn_del": "Удалить профиль",
        "lbl_idioma": "Язык:",
        "projetos_sufixo": "проектов",
        "aviso_selecao": "Выберите профиль из списка!",
        "aviso_nome": "Введите имя для профиля!",
        "erro_existe": "Профиль с таким именем уже существует!",
        "sucesso_criado": "Профиль '{nome}' успешно создан!",
        "sucesso_atalho": "Ярлык создан на рабочем столе:\nAntigravity - {nome}.lnk",
        "confirma_del": "Вы уверены, что хотите удалить профиль '{nome}' и все его данные?",
        "top_titulo": "Исключения проектов - {nome}",
        "top_aviso": "Отметьте проекты, которые нужно ИСКЛЮЧИТЬ (скрыть) для: {nome}\n(Остальные проекты доступны по умолчанию)",
        "filtrar": "Фильтр:",
        "marcar_todos": "Исключить все",
        "desmarcar_todos": "Открыть все (сбросить)",
        "btn_salvar": "Сохранить изменения",
        "btn_cancelar": "Отмена",
        "sucesso_proj": "Исключения проектов профиля '{nome}' успешно обновлены!",
        "info_sem_proj": "Проекты в системе не найдены!",
        "t_aviso": "Предупреждение",
        "t_erro": "Ошибка",
        "t_sucesso": "Успех",
        "t_confirma": "Подтверждение",
        "t_info": "Информация"
    }
}

IDIOMAS_NOMES = {
    "Português": "pt",
    "English": "en",
    "Español": "es",
    "Русский": "ru"
}

def carregar_idioma_salvo():
    if os.path.exists(CONFIG_FILE):
        try:
            with open(CONFIG_FILE, "r", encoding="utf-8") as f:
                d = json.load(f)
                return d.get("lang", "pt")
        except:
            pass
    return "pt"

def salvar_idioma(sigla):
    try:
        with open(CONFIG_FILE, "w", encoding="utf-8") as f:
            json.dump({"lang": sigla}, f)
    except:
        pass

idioma_atual = carregar_idioma_salvo()

def t(chave, **kwargs):
    dic = TEXTOS.get(idioma_atual, TEXTOS["pt"])
    val = dic.get(chave, chave)
    if kwargs:
        return val.format(**kwargs)
    return val

def obter_todos_projetos():
    projetos = {}
    pastas = [MASTER_DIR]
    if os.path.exists(PROFILES_DIR):
        for p in os.listdir(PROFILES_DIR):
            d = os.path.join(PROFILES_DIR, p, "home", ".gemini", "config", "projects")
            if os.path.exists(d):
                pastas.append(d)
    for d in pastas:
        if not os.path.exists(d):
            continue
        for f in os.listdir(d):
            if f.endswith(".json") and f not in ("outside-of-project.json", "default-cli-project.json"):
                caminho = os.path.join(d, f)
                try:
                    with open(caminho, "r", encoding="utf-8") as fp:
                        dados = json.load(fp)
                        # Filter out internal sidecars (like swarm/swarm_board)
                        if dados.get("sidecar") or dados.get("name") == "swarm/swarm_board":
                            continue
                        # Only include actual workspace projects that define projectResources
                        resources = dados.get("projectResources", {}).get("resources", [])
                        if not resources:
                            continue
                        nome = dados.get("name", f[:-5])
                        pid = dados.get("id", f[:-5])
                        if pid not in projetos:
                            projetos[pid] = {"id": pid, "name": nome, "filename": f, "src": caminho}
                except:
                    pass
    return projetos

def carregar_exclusoes(pasta_perfil):
    caminho = os.path.join(pasta_perfil, "exclusions.json")
    if os.path.exists(caminho):
        try:
            with open(caminho, "r", encoding="utf-8") as fp:
                dados = json.load(fp)
                if isinstance(dados, list):
                    return set(dados)
        except:
            pass
    return set()

def salvar_exclusoes(pasta_perfil, exclusoes_set):
    caminho = os.path.join(pasta_perfil, "exclusions.json")
    try:
        with open(caminho, "w", encoding="utf-8") as fp:
            json.dump(sorted(list(exclusoes_set)), fp, indent=2)
    except:
        pass

def sincronizar_perfil(pasta_perfil):
    pasta_proj = os.path.join(pasta_perfil, "home", ".gemini", "config", "projects")
    os.makedirs(pasta_proj, exist_ok=True)
    
    outside_file = os.path.join(pasta_proj, "outside-of-project.json")
    if not os.path.exists(outside_file):
        try:
            with open(outside_file, "w", encoding="utf-8") as fp:
                json.dump({"id": "outside-of-project", "name": "Outside of Project"}, fp)
        except:
            pass

    # Clean out orphan sidecar files (e.g., swarm/swarm_board) previously copied into this profile
    if os.path.exists(pasta_proj):
        for f in os.listdir(pasta_proj):
            if f.endswith(".json") and f not in ("outside-of-project.json", "default-cli-project.json"):
                caminho_f = os.path.join(pasta_proj, f)
                try:
                    with open(caminho_f, "r", encoding="utf-8") as fp:
                        d = json.load(fp)
                        if d.get("sidecar") or d.get("name") == "swarm/swarm_board":
                            fp.close()
                            os.remove(caminho_f)
                except:
                    pass

    todos = obter_todos_projetos()
    exclusoes = carregar_exclusoes(pasta_perfil)

    # By default, every valid project is shared unless explicitly excluded
    for pid, p_info in todos.items():
        destino = os.path.join(pasta_proj, p_info["filename"])
        if pid in exclusoes:
            if os.path.exists(destino):
                try:
                    os.remove(destino)
                except:
                    pass
        else:
            if not os.path.exists(destino):
                try:
                    shutil.copy2(p_info["src"], destino)
                except:
                    pass

def carregar_lista():
    lista.delete(0, tk.END)
    if os.path.exists(PROFILES_DIR):
        for pasta in sorted(os.listdir(PROFILES_DIR)):
            caminho = os.path.join(PROFILES_DIR, pasta)
            if os.path.isdir(caminho):
                d_proj = os.path.join(caminho, "home", ".gemini", "config", "projects")
                qtd = 0
                if os.path.exists(d_proj):
                    for f in os.listdir(d_proj):
                        if f.endswith(".json") and f not in ("outside-of-project.json", "default-cli-project.json"):
                            try:
                                with open(os.path.join(d_proj, f), "r", encoding="utf-8") as fp:
                                    d = json.load(fp)
                                    if not d.get("sidecar") and d.get("name") != "swarm/swarm_board":
                                        qtd += 1
                            except:
                                pass
                sufixo = t("projetos_sufixo")
                lista.insert(tk.END, f"{pasta}  ({qtd} {sufixo})")

def garantir_link_antigravity(pasta_perfil):
    master_ag = os.path.join(os.path.expanduser("~"), ".gemini", "antigravity")
    if not os.path.exists(master_ag):
        return
    ag_dir = os.path.join(pasta_perfil, "home", ".gemini", "antigravity")
    gemini_dir = os.path.join(pasta_perfil, "home", ".gemini")
    os.makedirs(gemini_dir, exist_ok=True)
    if os.path.islink(ag_dir):
        return
    if os.path.exists(ag_dir):
        summaries = os.path.join(ag_dir, "conversation_summaries.db")
        if os.path.exists(summaries) and os.path.getsize(summaries) > 30000:
            return
        try:
            shutil.rmtree(ag_dir, ignore_errors=True)
        except:
            pass
    try:
        subprocess.run(["cmd", "/c", "mklink", "/J", ag_dir, master_ag], capture_output=True)
    except:
        pass
    if not os.path.exists(ag_dir):
        try:
            os.makedirs(ag_dir, exist_ok=True)
            for f in ["conversation_summaries.db", "antigravity_state.pbtxt"]:
                src = os.path.join(master_ag, f)
                if os.path.exists(src):
                    shutil.copy2(src, os.path.join(ag_dir, f))
            for d in ["conversations", "brain"]:
                src = os.path.join(master_ag, d)
                dst = os.path.join(ag_dir, d)
                if os.path.exists(src) and not os.path.exists(dst):
                    shutil.copytree(src, dst)
        except:
            pass

def criar_perfil():
    nome = entrada_nome.get().strip()
    if not nome:
        messagebox.showwarning(t("t_aviso"), t("aviso_nome"))
        return
    pasta_perfil = os.path.join(PROFILES_DIR, nome)
    if os.path.exists(pasta_perfil):
        messagebox.showerror(t("t_erro"), t("erro_existe"))
        return
    pasta_proj = os.path.join(pasta_perfil, "home", ".gemini", "config", "projects")
    os.makedirs(os.path.join(pasta_perfil, "data"), exist_ok=True)
    os.makedirs(pasta_proj, exist_ok=True)
    garantir_link_antigravity(pasta_perfil)
    
    # By default, all projects are shared (empty exclusions)
    salvar_exclusoes(pasta_perfil, set())
    sincronizar_perfil(pasta_perfil)

    entrada_nome.delete(0, tk.END)
    carregar_lista()
    messagebox.showinfo(t("t_sucesso"), t("sucesso_criado", nome=nome))

def abrir_perfil():
    selecao = lista.curselection()
    if not selecao:
        messagebox.showwarning(t("t_aviso"), t("aviso_selecao"))
        return
    item = lista.get(selecao[0])
    nome = item.split("  (")[0]
    pasta_perfil = os.path.join(PROFILES_DIR, nome)
    data_dir = os.path.join(pasta_perfil, "data")
    home_dir = os.path.join(pasta_perfil, "home")
    
    # Sync projects before launch (so projects created in other profiles are available)
    sincronizar_perfil(pasta_perfil)
    garantir_link_antigravity(pasta_perfil)

    env = os.environ.copy()
    env["USERPROFILE"] = home_dir
    env["SSH_CONNECTION"] = "127.0.0.1 1234 127.0.0.1 22"
    env["SSH_CLIENT"] = "127.0.0.1 1234 22"
    cmd = [EXE_PATH, f"--user-data-dir={data_dir}"]
    subprocess.Popen(cmd, env=env, cwd=os.path.dirname(EXE_PATH), creationflags=subprocess.DETACHED_PROCESS | subprocess.CREATE_NEW_PROCESS_GROUP)

def criar_atalho():
    selecao = lista.curselection()
    if not selecao:
        messagebox.showwarning(t("t_aviso"), t("aviso_selecao"))
        return
    item = lista.get(selecao[0])
    nome = item.split("  (")[0]
    pasta_perfil = os.path.join(PROFILES_DIR, nome)
    data_dir = os.path.join(pasta_perfil, "data")
    home_dir = os.path.join(pasta_perfil, "home")
    sincronizar_perfil(pasta_perfil)
    garantir_link_antigravity(pasta_perfil)
    runner_script = os.path.join(pasta_perfil, "launch.vbs")
    vbs = f'''Set WshShell = CreateObject("WScript.Shell")
Set objEnv = WshShell.Environment("PROCESS")
objEnv("USERPROFILE") = "{home_dir}"
objEnv("SSH_CONNECTION") = "127.0.0.1 1234 127.0.0.1 22"
objEnv("SSH_CLIENT") = "127.0.0.1 1234 22"
WshShell.Run """{EXE_PATH}"" --user-data-dir=""{data_dir}""", 1, False
'''
    with open(runner_script, "w", encoding="utf-8") as f:
        f.write(vbs)
    desktop = os.path.join(os.path.expanduser("~"), "Desktop")
    shortcut_path = os.path.join(desktop, f"Antigravity - {nome}.lnk")
    ps = f'''
$sh = New-Object -ComObject WScript.Shell
$s = $sh.CreateShortcut('{shortcut_path}')
$s.TargetPath = 'wscript.exe'
$s.Arguments = '"{runner_script}"'
$s.WorkingDirectory = '{os.path.dirname(EXE_PATH)}'
$s.IconLocation = '{EXE_PATH},0'
$s.Description = 'Antigravity - {nome}'
$s.Save()
'''
    subprocess.run(["powershell", "-NoProfile", "-Command", ps], check=True)
    messagebox.showinfo(t("t_sucesso"), t("sucesso_atalho", nome=nome))

def excluir_perfil():
    selecao = lista.curselection()
    if not selecao:
        messagebox.showwarning(t("t_aviso"), t("aviso_selecao"))
        return
    item = lista.get(selecao[0])
    nome = item.split("  (")[0]
    if messagebox.askyesno(t("t_confirma"), t("confirma_del", nome=nome)):
        shutil.rmtree(os.path.join(PROFILES_DIR, nome), ignore_errors=True)
        carregar_lista()

def gerenciar_projetos():
    selecao = lista.curselection()
    if not selecao:
        messagebox.showwarning(t("t_aviso"), t("aviso_selecao"))
        return
    item = lista.get(selecao[0])
    nome = item.split("  (")[0]
    pasta_perfil = os.path.join(PROFILES_DIR, nome)
    pasta_proj_perfil = os.path.join(pasta_perfil, "home", ".gemini", "config", "projects")
    os.makedirs(pasta_proj_perfil, exist_ok=True)

    todos = obter_todos_projetos()
    if not todos:
        messagebox.showinfo(t("t_info"), t("info_sem_proj"))
        return

    exclusoes_atuais = carregar_exclusoes(pasta_perfil)

    top = tk.Toplevel(janela)
    top.title(t("top_titulo", nome=nome))
    top.geometry("490x540")
    top.transient(janela)

    frame_topo_top = tk.Frame(top, padx=10, pady=5)
    frame_topo_top.pack(fill="x")

    lbl_aviso = tk.Label(frame_topo_top, text=t("top_aviso", nome=nome), font=("Arial", 9, "bold"), justify="left")
    lbl_aviso.pack(anchor="w", pady=(0, 5))

    frame_busca = tk.Frame(frame_topo_top)
    frame_busca.pack(fill="x", pady=2)
    tk.Label(frame_busca, text=t("filtrar")).pack(side="left")
    entrada_filtro = tk.Entry(frame_busca)
    entrada_filtro.pack(side="left", fill="x", expand=True, padx=5)

    frame_acoes = tk.Frame(frame_topo_top)
    frame_acoes.pack(fill="x", pady=4)

    def marcar_todos():
        for pid in vars_map:
            vars_map[pid].set(True)

    def desmarcar_todos():
        for pid in vars_map:
            vars_map[pid].set(False)

    btn_marcar = tk.Button(frame_acoes, text=t("marcar_todos"), command=marcar_todos, width=15)
    btn_marcar.pack(side="left", padx=2)

    btn_desmarcar = tk.Button(frame_acoes, text=t("desmarcar_todos"), command=desmarcar_todos, width=20)
    btn_desmarcar.pack(side="left", padx=2)

    frame_conteudo = tk.Frame(top, padx=10, pady=5)
    frame_conteudo.pack(fill="both", expand=True)

    canvas = tk.Canvas(frame_conteudo, borderwidth=1, relief="sunken")
    scroll = tk.Scrollbar(frame_conteudo, orient="vertical", command=canvas.yview)
    frame_scroll = tk.Frame(canvas)

    frame_scroll.bind("<Configure>", lambda e: canvas.configure(scrollregion=canvas.bbox("all")))
    canvas.create_window((0, 0), window=frame_scroll, anchor="nw")
    canvas.configure(yscrollcommand=scroll.set)

    canvas.pack(side="left", fill="both", expand=True)
    scroll.pack(side="right", fill="y")

    def rolar(evento):
        canvas.yview_scroll(int(-1 * (evento.delta / 120)), "units")
    canvas.bind_all("<MouseWheel>", rolar)

    vars_map = {}
    widgets_map = {}

    projetos_ordenados = sorted(todos.values(), key=lambda x: x["name"].lower())

    for p in projetos_ordenados:
        pid = p["id"]
        esta_excluido = pid in exclusoes_atuais
        var = tk.BooleanVar(value=esta_excluido)
        vars_map[pid] = var
        cb = tk.Checkbutton(frame_scroll, text=f"[Exclude]  {p['name']}", variable=var, font=("Arial", 9))
        cb.pack(anchor="w", padx=4, pady=2)
        widgets_map[pid] = (cb, p["name"].lower())

    def filtrar(evento=None):
        texto = entrada_filtro.get().strip().lower()
        for pid, (cb, nome_p) in widgets_map.items():
            if texto in nome_p:
                cb.pack(anchor="w", padx=4, pady=2)
            else:
                cb.pack_forget()

    entrada_filtro.bind("<KeyRelease>", filtrar)

    frame_rodape = tk.Frame(top, padx=10, pady=10)
    frame_rodape.pack(fill="x")

    def salvar():
        novas_exclusoes = {pid for pid, var in vars_map.items() if var.get()}
        salvar_exclusoes(pasta_perfil, novas_exclusoes)
        sincronizar_perfil(pasta_perfil)
        canvas.unbind_all("<MouseWheel>")
        top.destroy()
        carregar_lista()
        messagebox.showinfo(t("t_sucesso"), t("sucesso_proj", nome=nome))

    btn_salvar = tk.Button(frame_rodape, text=t("btn_salvar"), command=salvar, width=18, bg="#d9d9d9", height=2)
    btn_salvar.pack(side="left", padx=5)

    btn_cancelar = tk.Button(frame_rodape, text=t("btn_cancelar"), command=lambda: (canvas.unbind_all("<MouseWheel>"), top.destroy()), width=12, height=2)
    btn_cancelar.pack(side="right", padx=5)

def atualizar_textos_interface():
    janela.title(t("title"))
    frame_topo.config(text=t("frame_create"))
    lbl_nome.config(text=t("lbl_nome"))
    btn_add.config(text=t("btn_add"))
    lbl_info_compartilhar.config(text=f"ℹ  {t('chk_copiar')}")
    frame_meio.config(text=t("frame_perfis"))
    btn_abrir.config(text=t("btn_abrir"))
    btn_proj.config(text=t("btn_proj"))
    btn_atalho.config(text=t("btn_atalho"))
    btn_del.config(text=t("btn_del"))
    lbl_idioma.config(text=t("lbl_idioma"))
    carregar_lista()

def mudar_idioma(escolha):
    global idioma_atual
    sigla = IDIOMAS_NOMES.get(escolha, "pt")
    idioma_atual = sigla
    salvar_idioma(sigla)
    atualizar_textos_interface()

if __name__ == "__main__":
    janela = tk.Tk()
    janela.geometry("500x510")
    janela.resizable(False, False)

    ico_caminho = os.path.join(BASE_DIR, "icon.ico")
    if os.path.exists(ico_caminho):
        try:
            janela.iconbitmap(ico_caminho)
        except:
            pass
    elif os.path.exists(EXE_PATH):
        try:
            janela.iconbitmap(EXE_PATH)
        except:
            pass

    frame_idioma = tk.Frame(janela, padx=10, pady=5)
    frame_idioma.pack(fill="x")

    lbl_idioma = tk.Label(frame_idioma, text=t("lbl_idioma"), font=("Arial", 9))
    lbl_idioma.pack(side="left")

    var_idioma = tk.StringVar(janela)
    nome_inverso = {v: k for k, v in IDIOMAS_NOMES.items()}
    var_idioma.set(nome_inverso.get(idioma_atual, "Português"))

    opt_idioma = tk.OptionMenu(frame_idioma, var_idioma, *IDIOMAS_NOMES.keys(), command=mudar_idioma)
    opt_idioma.config(font=("Arial", 9), width=12)
    opt_idioma.pack(side="left", padx=5)

    frame_topo = tk.LabelFrame(janela, text=t("frame_create"), padx=10, pady=10)
    frame_topo.pack(padx=10, pady=5, fill="x")

    lbl_nome = tk.Label(frame_topo, text=t("lbl_nome"))
    lbl_nome.grid(row=0, column=0, sticky="w")

    entrada_nome = tk.Entry(frame_topo, width=28)
    entrada_nome.grid(row=0, column=1, padx=5)

    btn_add = tk.Button(frame_topo, text=t("btn_add"), command=criar_perfil, width=10)
    btn_add.grid(row=0, column=2, padx=5)

    lbl_info_compartilhar = tk.Label(frame_topo, text=f"ℹ  {t('chk_copiar')}", font=("Arial", 8, "italic"), fg="#555555")
    lbl_info_compartilhar.grid(row=1, column=0, columnspan=3, sticky="w", pady=(6, 0))

    frame_meio = tk.LabelFrame(janela, text=t("frame_perfis"), padx=10, pady=10)
    frame_meio.pack(padx=10, pady=5, fill="both", expand=True)

    scrollbar = tk.Scrollbar(frame_meio)
    scrollbar.pack(side="right", fill="y")

    lista = tk.Listbox(frame_meio, yscrollcommand=scrollbar.set, font=("Arial", 10), height=7)
    lista.pack(side="left", fill="both", expand=True)
    scrollbar.config(command=lista.yview)

    frame_botoes = tk.Frame(janela, padx=10, pady=5)
    frame_botoes.pack(fill="x")

    btn_abrir = tk.Button(frame_botoes, text=t("btn_abrir"), command=abrir_perfil, height=2, bg="#d9d9d9")
    btn_abrir.pack(fill="x", pady=2)

    btn_proj = tk.Button(frame_botoes, text=t("btn_proj"), command=gerenciar_projetos, height=1)
    btn_proj.pack(fill="x", pady=2)

    btn_atalho = tk.Button(frame_botoes, text=t("btn_atalho"), command=criar_atalho)
    btn_atalho.pack(fill="x", pady=2)

    btn_del = tk.Button(frame_botoes, text=t("btn_del"), command=excluir_perfil)
    btn_del.pack(fill="x", pady=2)

    atualizar_textos_interface()
    janela.mainloop()

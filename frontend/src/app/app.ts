import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ContaApi, mensagemDeErro } from './conta-api';

@Component({
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  selector: 'app-root',
  templateUrl: './app.html',
})
export class App {
  private readonly api = inject(ContaApi);
  private readonly router = inject(Router);
  readonly sessao = this.api.sessao;
  readonly saindo = signal(false);
  readonly erro = signal('');

  constructor() {
    void this.api.atualizarSessao().catch(() => {
      // O formulário informa erros de conexão quando o usuário tenta entrar.
    });
  }

  pularParaConteudo(evento: MouseEvent, conteudo: HTMLElement): void {
    evento.preventDefault();
    conteudo.focus();
  }

  async sair(): Promise<void> {
    if (this.saindo()) return;
    this.erro.set('');
    this.saindo.set(true);
    try {
      await this.api.sair();
      await this.router.navigateByUrl('/conta/entrar');
    } catch (error) {
      this.erro.set(mensagemDeErro(error, 'Não foi possível encerrar a sessão.'));
    } finally {
      this.saindo.set(false);
    }
  }
}

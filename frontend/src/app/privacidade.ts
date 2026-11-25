import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ContaApi, mensagemDeErro } from './conta-api';

@Component({
  imports: [FormsModule, RouterLink],
  templateUrl: './privacidade.html',
})
export class Privacidade {
  private readonly conta = inject(ContaApi);
  readonly sessao = this.conta.sessao;
  readonly solicitar = inject(ActivatedRoute).snapshot.data['solicitar'] === true;
  readonly email = signal<string | null>(null);
  readonly protocolo = signal('');
  readonly erro = signal('');
  readonly enviando = signal(false);
  somenteObra = false;
  ilustracaoId = inject(ActivatedRoute).snapshot.queryParamMap.get('ilustracao') ?? '';
  senha = '';

  constructor() {
    this.somenteObra = !!this.ilustracaoId;
    const http = inject(HttpClient);
    if (!this.solicitar) {
      void firstValueFrom(http.get<{ email: string | null }>('/api/privacidade'))
        .then(({ email }) => this.email.set(email))
        .catch(() => this.erro.set('Não foi possível carregar o contato de privacidade.'));
    }
  }

  async enviar(): Promise<void> {
    if (this.enviando() || this.protocolo() || !this.senha) return;
    const ilustracaoId = this.somenteObra ? this.ilustracaoId.trim() : null;
    if (
      ilustracaoId !== null &&
      !/^[\da-f]{8}(?:-[\da-f]{4}){3}-[\da-f]{12}$/i.test(ilustracaoId)
    ) {
      this.erro.set('Informe o identificador da ilustração no formato exibido após o envio.');
      return;
    }
    this.enviando.set(true);
    this.erro.set('');
    try {
      const { id } = await this.conta.mutarComCsrf<{ id: string }>(
        'POST',
        '/api/privacidade/exclusao',
        {
          ilustracaoId,
          senha: this.senha,
        },
      );
      this.protocolo.set(id);
      if (ilustracaoId === null) this.conta.limparSessao();
    } catch (error) {
      const status = error instanceof HttpErrorResponse ? error.status : null;
      const detalhe =
        status === 403
          ? 'Não foi possível confirmar sua senha.'
          : status === 404
            ? 'A ilustração não foi encontrada na sua conta.'
            : 'Não foi possível registrar seu pedido.';
      this.erro.set(mensagemDeErro(error, detalhe));
    } finally {
      this.senha = '';
      this.enviando.set(false);
    }
  }
}

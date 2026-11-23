import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ContaApi, mensagemDeErro } from './conta-api';
import { GaleriaApi } from './galeria-api';

const LIMITE_ARQUIVO = 10_000_000;
const TIPOS = ['image/jpeg', 'image/png', 'image/webp'];

@Component({
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './publicar.html',
})
export class Publicar {
  private readonly conta = inject(ContaApi);
  private readonly api = inject(GaleriaApi);
  private readonly router = inject(Router);
  readonly sessao = this.conta.sessao;
  readonly carregando = signal(true);
  readonly enviando = signal(false);
  readonly selecionado = signal<File | null>(null);
  readonly enviado = signal(false);
  readonly erro = signal('');
  readonly erroArquivo = signal('');
  readonly formulario = new FormGroup({
    titulo: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/\S/), Validators.maxLength(200)],
    }),
    descricao: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
    categoria: new FormControl<'ORIGINAL' | 'FANART'>('ORIGINAL', { nonNullable: true }),
  });

  constructor() {
    void this.checarSessao();
  }

  selecionar(evento: Event): void {
    const input = evento.target as HTMLInputElement;
    const arquivo = input.files?.item(0) ?? null;
    this.selecionado.set(null);
    this.erroArquivo.set('');
    if (!arquivo) return;

    if (!TIPOS.includes(arquivo.type) || !/\.(jpe?g|png|webp)$/i.test(arquivo.name)) {
      this.erroArquivo.set('Selecione um arquivo JPEG, PNG ou WebP.');
    } else if (!arquivo.size || arquivo.size > LIMITE_ARQUIVO) {
      this.erroArquivo.set('Selecione uma imagem de até 10 MB.');
    } else {
      this.selecionado.set(arquivo);
      return;
    }
    input.value = '';
  }

  async enviar(): Promise<void> {
    this.formulario.markAllAsTouched();
    if (this.enviando() || !this.sessao()?.emailConfirmado) return;
    const arquivo = this.selecionado();
    if (!arquivo && !this.erroArquivo()) this.erroArquivo.set('Selecione uma imagem para enviar.');
    if (this.formulario.invalid || !arquivo) return;

    this.erro.set('');
    this.erroArquivo.set('');
    this.enviando.set(true);
    const dados = this.formulario.getRawValue();
    const formulario = new FormData();
    formulario.append('titulo', dados.titulo.trim());
    formulario.append('descricao', dados.descricao.trim());
    formulario.append('categoria', dados.categoria);
    formulario.append('imagem', arquivo, arquivo.name);
    try {
      await this.api.publicar(formulario);
      this.enviado.set(true);
      this.selecionado.set(null);
      this.formulario.reset();
    } catch (erro) {
      if (erro instanceof HttpErrorResponse) {
        switch (erro.status) {
          case 401:
            await this.router.navigateByUrl('/conta/entrar');
            return;
          case 403:
            this.erro.set('A sessão atual precisa ser de um artista com e-mail confirmado.');
            return;
          case 409:
            this.erro.set('Você atingiu o limite de cinco ilustrações pendentes ou aprovadas.');
            return;
          case 413:
          case 415:
          case 400:
            this.erroArquivo.set(this.mensagemDeArquivo(erro.status));
            return;
        }
      }
      this.erro.set(mensagemDeErro(erro, 'Não foi possível enviar a ilustração. Tente novamente.'));
    } finally {
      this.enviando.set(false);
    }
  }

  private async checarSessao(): Promise<void> {
    try {
      if (!(await this.conta.atualizarSessao())) await this.router.navigateByUrl('/conta/entrar');
    } catch (erro) {
      this.erro.set(mensagemDeErro(erro, 'Não foi possível verificar sua sessão.'));
    } finally {
      this.carregando.set(false);
    }
  }

  private mensagemDeArquivo(status: number): string {
    if (status === 413) return 'A imagem ultrapassou o limite. Envie uma versão menor.';
    if (status === 415) return 'O arquivo precisa ser JPEG, PNG ou WebP estático.';
    return 'Confira os campos e selecione uma imagem válida de até 4.096 pixels por lado.';
  }
}

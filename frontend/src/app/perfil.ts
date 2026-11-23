import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ContaApi, Perfil, mensagemDeErro } from './conta-api';

@Component({
  imports: [ReactiveFormsModule, RouterLink],
  selector: 'app-perfil',
  templateUrl: './perfil.html',
})
export class PerfilArtista {
  private readonly api = inject(ContaApi);
  private readonly rota = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private carregamento = 0;

  readonly editavel = this.rota.snapshot.data['editavel'] === true;
  readonly sessao = this.api.sessao;
  readonly perfil = signal<Perfil | null>(null);
  readonly buscando = signal(true);
  readonly salvando = signal(false);
  readonly erro = signal('');
  readonly aviso = signal('');
  readonly formulario = new FormGroup({
    nomePublico: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200), Validators.pattern(/\S/)],
    }),
    bio: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
  });

  constructor() {
    this.rota.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      void this.carregar(params.get('id'));
    });
  }

  async salvar(): Promise<void> {
    this.formulario.markAllAsTouched();
    if (!this.editavel || this.formulario.invalid || this.salvando()) return;

    this.erro.set('');
    this.aviso.set('');
    this.salvando.set(true);
    const { nomePublico, bio } = this.formulario.getRawValue();
    try {
      await this.api.editarPerfil(nomePublico.trim(), bio.trim() || null);
      const perfil = this.perfil();
      if (perfil) {
        this.perfil.set({ ...perfil, nomePublico: nomePublico.trim(), bio: bio.trim() || null });
      }
      this.aviso.set('Perfil atualizado.');
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        await this.router.navigateByUrl('/conta/entrar');
        return;
      }
      this.erro.set(mensagemDeErro(error, 'Não foi possível salvar o perfil. Tente novamente.'));
    } finally {
      this.salvando.set(false);
    }
  }

  iniciais(nome: string): string {
    const palavras = nome.trim().split(/\s+/u);
    const selecionadas =
      palavras.length > 1 ? [palavras[0], palavras[palavras.length - 1]] : palavras;
    return selecionadas
      .map((palavra) => Array.from(palavra)[0] ?? '')
      .join('')
      .toLocaleUpperCase('pt-BR');
  }

  private async carregar(id: string | null): Promise<void> {
    const tentativa = ++this.carregamento;
    this.buscando.set(true);
    this.perfil.set(null);
    this.erro.set('');
    try {
      if (this.editavel) {
        const sessao = await this.api.atualizarSessao();
        if (!sessao) {
          await this.router.navigateByUrl('/conta/entrar');
          return;
        }
        id = sessao.id;
      }

      if (!id) {
        this.erro.set('Perfil de artista indisponível.');
        return;
      }

      const perfil = await this.api.perfil(id);
      if (tentativa !== this.carregamento) return;
      this.perfil.set(perfil);
      this.formulario.reset({ nomePublico: perfil.nomePublico, bio: perfil.bio ?? '' });
    } catch (error) {
      if (tentativa === this.carregamento) {
        this.erro.set(mensagemDeErro(error, 'Perfil de artista indisponível.'));
      }
    } finally {
      if (tentativa === this.carregamento) this.buscando.set(false);
    }
  }
}

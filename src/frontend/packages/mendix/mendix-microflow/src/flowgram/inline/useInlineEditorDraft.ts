import { useCallback, useEffect, useRef, useState } from "react";

export type InlineEditorDraft = Record<string, unknown>;
export type FieldValidator = (value: unknown) => string | null;

export interface InlineEditorDraftState {
  draft: InlineEditorDraft;
  fieldErrors: Record<string, string>;
  isDraftValid: () => boolean;
  updateField: (key: string, value: unknown) => void;
  resetDraft: (initial?: InlineEditorDraft) => void;
}

export function useInlineEditorDraft(
  initial: InlineEditorDraft,
  validators: Record<string, FieldValidator> = {},
  registerDraftValidator: ((fn: (() => { valid: boolean; summary: string }) | null) => void) | undefined = undefined,
): InlineEditorDraftState {
  const [draft, setDraft] = useState<InlineEditorDraft>(initial);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const validatorsRef = useRef(validators);
  validatorsRef.current = validators;

  const isDraftValid = useCallback((): boolean => {
    return Object.values(fieldErrors).every(e => !e);
  }, [fieldErrors]);

  const getDraftValidation = useCallback((): { valid: boolean; summary: string } => {
    const firstError = Object.values(fieldErrors).find(e => e);
    return { valid: !firstError, summary: firstError ?? "" };
  }, [fieldErrors]);

  useEffect(() => {
    if (registerDraftValidator) {
      registerDraftValidator(getDraftValidation);
      return () => registerDraftValidator(null);
    }
  }, [registerDraftValidator, getDraftValidation]);

  const updateField = useCallback((key: string, value: unknown) => {
    setDraft(prev => ({ ...prev, [key]: value }));
    const validator = validatorsRef.current[key];
    if (validator) {
      const error = validator(value) ?? "";
      setFieldErrors(prev => ({ ...prev, [key]: error }));
    }
  }, []);

  const resetDraft = useCallback((next?: InlineEditorDraft) => {
    setDraft(next ?? initial);
    setFieldErrors({});
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return { draft, fieldErrors, isDraftValid, updateField, resetDraft };
}

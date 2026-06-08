declare module 'sm-crypto-v2' {
  export const sm2: {
    doEncrypt(
      msg: string,
      publicKey: string,
      cipherMode?: 0 | 1,
    ): string;
  };
}

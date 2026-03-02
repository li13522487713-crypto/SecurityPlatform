/**
 * 判断是否是外链
 * @param path
 * @returns {boolean}
 */
export function isExternal(path: string) {
  return /^(https?:|mailto:|tel:)/.test(path)
}

/**
 * 判断是否是合法网址
 * @param url
 * @returns {boolean}
 */
export function validURL(url: string) {
  const reg = /^(https?|ftp):\/\/([a-zA-Z0-9.-]+(:[a-zA-Z0-9.&%$-]+)*@)*((25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9][0-9]?)(\.(25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9])){3}|([a-zA-Z0-9-]+\.)*[a-zA-Z0-9-]+\.(com|edu|gov|int|mil|net|org|biz|arpa|info|name|pro|aero|coop|museum|[a-zA-Z]{2}))(:[0-9]+)*(\/($|[a-zA-Z0-9.,?'\\+&%$#=~_-]+))*$/
  return reg.test(url)
}

/**
 * 判断是否全是小写字母
 * @param str
 * @returns {boolean}
 */
export function validLowerCase(str: string) {
  const reg = /^[a-z]+$/
  return reg.test(str)
}

/**
 * 判断是否全是大写字母
 * @param str
 * @returns {boolean}
 */
export function validUpperCase(str: string) {
  const reg = /^[A-Z]+$/
  return reg.test(str)
}

/**
 * 判断是否全是字母
 * @param str
 * @returns {boolean}
 */
export function validAlphabets(str: string) {
  const reg = /^[A-Za-z]+$/
  return reg.test(str)
}

/**
 * 判断是否是邮箱
 * @param email
 * @returns {boolean}
 */
export function validEmail(email: string) {
  const reg = /^(([^<>()\[\]\\.,;:\s@"]+(\.[^<>()\[\]\\.,;:\s@"]+)*)|(".+"))@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\])|(([a-zA-Z\-0-9]+\.)+[a-zA-Z]{2,}))$/
  return reg.test(email)
}

/**
 * 判断是否是字符串
 * @param str
 * @returns {boolean}
 */
export function isString(str: any) {
  if (typeof str === 'string' || str instanceof String) {
    return true
  }
  return false
}

/**
 * 判断是否是数组
 * @param arg
 * @returns {boolean}
 */
export function isArray(arg: any) {
  if (typeof Array.isArray === 'undefined') {
    return Object.prototype.toString.call(arg) === '[object Array]'
  }
  return Array.isArray(arg)
}

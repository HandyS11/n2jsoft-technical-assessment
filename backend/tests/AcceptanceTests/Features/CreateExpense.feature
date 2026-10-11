Feature: Add an expense to a report

  Background:
    Given the following users exist
      | First name | Last name | Monthly quota |
      | Juste      | Leblanc   | 2             |
    And the following expense reports exist
      | User          | Month   |
      | Juste Leblanc | 2025-10 |

  Scenario: Adding an expense answers with the expense, where to find it and its version
    When I add the following expense to "Juste Leblanc - Octobre 2025"
      | Date       | Description | Amount | Store | Street           | Postal code | City  |
      | 2025-10-15 | Taxi        | 23.50  | G7    | 12 rue de Rivoli | 75001       | Paris |
    Then the response status is 201
    And the response locates the created expense
    And the response ETag is the version of the expense returned
    And the expense returned is
      | Date       | Description | Amount | Store | Street           | Postal code | City  |
      | 2025-10-15 | Taxi        | 23.50  | G7    | 12 rue de Rivoli | 75001       | Paris |

  Scenario: The store name is optional
    When I add the following expense to "Juste Leblanc - Octobre 2025"
      | Date       | Description | Street           | Postal code | City  |
      | 2025-10-15 | Parking     | 12 rue de Rivoli | 75001       | Paris |
    Then the response status is 201
    And the expense returned is
      | Date       | Description | Amount | Store | Street           | Postal code | City  |
      | 2025-10-15 | Parking     | 10.00  |       | 12 rue de Rivoli | 75001       | Paris |

  Scenario Outline: The first and the last day of the report's month are accepted
    When I add an expense dated "<date>" to "Juste Leblanc - Octobre 2025"
    Then the response status is 201

    Examples:
      | date       |
      | 2025-10-01 |
      | 2025-10-31 |

  Scenario Outline: A date outside the report's month is refused
    When I add an expense dated "<date>" to "Juste Leblanc - Octobre 2025"
    Then the response is a business rule violation with the code "expense.date_outside_report_month"

    Examples:
      | date       |
      | 2025-09-30 |
      | 2025-11-01 |

  Scenario: An expense beyond the monthly quota is refused
    Given the following expenses exist in "Juste Leblanc - Octobre 2025"
      | Date       | Description |
      | 2025-10-01 | Hôtel       |
      | 2025-10-02 | Train       |
    When I add an expense dated "2025-10-15" to "Juste Leblanc - Octobre 2025"
    Then the response is a business rule violation with the code "expense.quota_exceeded"
    When I list the expenses of "Juste Leblanc - Octobre 2025"
    Then the listing reports 2 expenses in total

  Scenario: A deleted expense frees a slot of the quota
    Given the following expenses exist in "Juste Leblanc - Octobre 2025"
      | Date       | Description | Status  |
      | 2025-10-01 | Hôtel       |         |
      | 2025-10-02 | Train       | Deleted |
    When I add an expense dated "2025-10-15" to "Juste Leblanc - Octobre 2025"
    Then the response status is 201

  Scenario: Two requests adding the last expense the quota allows at the same time add it once
    Given the following expenses exist in "Juste Leblanc - Octobre 2025"
      | Date       | Description |
      | 2025-10-01 | Hôtel       |
    When two requests add an expense to "Juste Leblanc - Octobre 2025" at the same time
    Then one request is accepted and the other is refused with the code "expense.quota_exceeded"
    When I list the expenses of "Juste Leblanc - Octobre 2025"
    Then the listing reports 2 expenses in total

  Scenario: An inactive user's report still takes expenses
    When I deactivate the user "Juste Leblanc"
    And I add an expense dated "2025-10-15" to "Juste Leblanc - Octobre 2025"
    Then the response status is 201

  Scenario: A deleted user's report takes no more expenses
    When I delete the user "Juste Leblanc"
    And I add an expense dated "2025-10-15" to "Juste Leblanc - Octobre 2025"
    Then the response is a business rule violation with the code "expense_report.deleted_user"

  Scenario: A user deleted while an expense is added to their report is seen by the addition
    When the user "Juste Leblanc" is deleted while an expense is added to "Juste Leblanc - Octobre 2025"
    Then the response is a business rule violation with the code "expense_report.deleted_user"
    When I list the expenses of "Juste Leblanc - Octobre 2025"
    Then no expense is listed

  Scenario: Adding an expense to an unknown report answers not found
    When I add an expense to an unknown expense report
    Then the response is a problem with status 404

  Scenario Outline: Adding an expense rejects an invalid <field>
    When I add an expense to "Juste Leblanc - Octobre 2025" whose <field> is <value>
    Then the response is a problem with status 400

    Examples:
      | field                     | value                                                   |
      | date                      | null                                                    |
      | date                      | "15/10/2025"                                            |
      | description               | null                                                    |
      | description               | "   "                                                   |
      | description               | "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"   |
      | amount                    | null                                                    |
      | amount                    | 0                                                       |
      | amount                    | -5                                                      |
      | amount                    | 12.345                                                  |
      | amount                    | 1000000                                                 |
      | billingAddress            | null                                                    |
      | billingAddress.storeName  | "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" |
      | billingAddress.street     | ""                                                      |
      | billingAddress.postalCode | "7500"                                                  |
      | billingAddress.city       | null                                                    |

Feature: Create a user

  Scenario: Creating a user answers with the new active user and where to find it
    When I create the user
      | First name | Last name | Street            | Postal code | City     | Monthly quota |
      | Jeanne     | Martin    | 5 rue Victor Hugo | 33000       | Bordeaux | 8             |
    Then the response status is 201
    And the response locates the created user
    And the response ETag is the version of the user returned
    And the user returned is
      | First name | Last name | Street            | Postal code | City     | Monthly quota | Status |
      | Jeanne     | Martin    | 5 rue Victor Hugo | 33000       | Bordeaux | 8             | Active |

  Scenario: The created user appears in the list
    When I create the user
      | First name | Last name | Street            | Postal code | City     | Monthly quota |
      | Jeanne     | Martin    | 5 rue Victor Hugo | 33000       | Bordeaux | 8             |
    And I list the users
    Then the users listed are
      | Name          |
      | Jeanne Martin |

  Scenario: Surrounding spaces are not kept
    When I create a user whose firstName is "  Jeanne  "
    Then the response status is 201
    And the user returned is
      | First name | Last name | Street            | Postal code | City     | Monthly quota | Status |
      | Jeanne     | Martin    | 5 rue Victor Hugo | 33000       | Bordeaux | 8             | Active |

  Scenario Outline: Creating a user rejects an invalid <field>
    When I create a user whose <field> is <value>
    Then the response is a problem with status 400

    Examples:
      | field               | value   |
      | firstName           | ""      |
      | firstName           | "   "   |
      | lastName            | ""      |
      | lastName            | null    |
      | address             | null    |
      | address.street      | "   "   |
      | address.postalCode  | "7500"  |
      | address.postalCode  | "75O01" |
      | address.postalCode  | "٧٥٠٠١" |
      | address.city        | ""      |
      | monthlyExpenseQuota | 0       |
      | monthlyExpenseQuota | -1      |
